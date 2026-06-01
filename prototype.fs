#nowarn "3391"

module GridSiege

open System
open System.Numerics
open Raylib_cs

// ============================================================
//  1. constants and types
// ============================================================
[<Literal>]
let ROWS = 5
[<Literal>]
let COLS = 9

// 셀 배치 기준값
let CELL_SIZE = 90
let MARGIN_X = 150
let MARGIN_Y = 135
let UI_HEIGHT = 108
let INITIAL_MOBS_PER_WAVE = 3
let MOBS_PER_WAVE_INCREASE = 2

// type definitions
type TowerType = Basic | Rapid | Area
type MobAction = Idle | Attacking | Hurting
type MobType = BasicMob | RangedMob | TankerMob
type GameStatus = Playing | GameOver

// tower state
type Tower = {
    Type: TowerType
    mutable HP: int
    MaxHP: int
    Row: int
    Col: int
    mutable AttackTimer: float32
    mutable AnimFrame: int
    mutable AnimTimer: float32
    mutable IsAttacking: bool
}

// projectile state
type Projectile = {
    mutable Pos: Vector2
    TargetId: int
    TargetIds: int list
    TargetRow: int
    TargetCol: int
    TargetPos: Vector2
    Type: TowerType
    Damage: int
    Speed: float32
    IsArea: bool
}
//mob's projectile state
type MobProjectile = {
    mutable Pos: Vector2
    TargetRow: int
    TargetCol: int
    TargetPos: Vector2
    Damage: int
    Speed: float32
    IsCastleTarget: bool
}

// mob state
type Mob = {
    Id: int
    Type: MobType
    mutable HP: int
    MaxHP: int
    mutable Row: int
    mutable Col: int
    mutable VisualX: float32
    mutable AttackTimer: float32
    mutable MoveTimer: float32
    mutable Action: MobAction
    mutable CurrentFrame: int
    mutable FrameTimer: float32
}


type MobTextures = { Idle: Texture2D; Atk: Texture2D; Hurt: Texture2D }

// ============================================================
//  2. assets / helpers
// ============================================================

let mutable bgTexture = Texture2D()
let mutable spriteSheet = Texture2D()


let mutable texBasic = { Idle = Texture2D(); Atk = Texture2D(); Hurt = Texture2D() }
let mutable texRanged = { Idle = Texture2D(); Atk = Texture2D(); Hurt = Texture2D() }
let mutable texTanker = { Idle = Texture2D(); Atk = Texture2D(); Hurt = Texture2D() }


let mutable texBasicTower = Texture2D()
let mutable texRapidTower = Texture2D()
let mutable texAreaTower = Texture2D()
let mutable texBullets = Texture2D()
let mutable texCastle = Texture2D()
let mutable uiFont = Font()
let CASTLE_MAX_HP = 1000.0f // 체력 비율 계산용

// texture load chekc
let textureLoaded (tex: Texture2D) =
    tex.Width > 0 && tex.Height > 0

// unload tc=exture
let unloadIfLoaded (tex: Texture2D) =
    if textureLoaded tex then
        Raylib.UnloadTexture(tex)

let unloadMobTextures (textures: MobTextures) =
    unloadIfLoaded textures.Idle
    unloadIfLoaded textures.Atk
    unloadIfLoaded textures.Hurt

let drawUiText (text: string) (x: int) (y: int) (fontSize: float32) (spacing: float32) (color: Color) =
    if Raylib.IsFontValid(uiFont) = true then
        Raylib.DrawTextEx(uiFont, text, Vector2(float32 x, float32 y), fontSize, spacing, color)
    else
        Raylib.DrawText(text, x, y, int fontSize, color)

// transparent 
let loadTextureWithWhiteTransparent (path: string) =
    let mutable image = Raylib.LoadImage(path)
    Raylib.ImageColorReplace(&image, Color.White, Color.Blank)
    let texture = Raylib.LoadTextureFromImage(image)
    Raylib.UnloadImage(image)
    texture

// mob action select
let getMobCurrentTexture mType mAction =
    match mType with
    | BasicMob ->
        match mAction with
        | Idle -> texBasic.Idle
        | Attacking -> texBasic.Atk
        | Hurting -> texBasic.Hurt
    | RangedMob ->
        match mAction with
        | Idle -> texRanged.Idle
        | Attacking -> texRanged.Atk
        | Hurting -> texRanged.Hurt
    | TankerMob ->
        match mAction with
        | Idle -> texTanker.Idle
        | Attacking -> texTanker.Atk
        | Hurting -> texTanker.Hurt

// tower texture
let getTowerTexture = function
    | Basic -> texBasicTower
    | Rapid -> texRapidTower
    | Area -> texAreaTower

// ============================================================
//  3. stats,...etc
// ============================================================
// tower stats
let getTowerStats = function
    | Basic -> {| Cost = 50; ATK = 25; HP = 100; Cooldown = 1.0f |}
    | Rapid -> {| Cost = 100; ATK = 20; HP = 70; Cooldown = 0.5f |}
    | Area -> {| Cost = 150; ATK = 50; HP = 150; Cooldown = 2.5f |}

// mob stats
let getMobStats = function
    | BasicMob -> {| Reward = 8; ATK = 10; HP = 150; Speed = 1.0f; Range = 1 |}
    | RangedMob -> {| Reward = 16; ATK = 30; HP = 100; Speed = 0.5f; Range = 3 |}
    | TankerMob -> {| Reward = 24; ATK = 25; HP = 800; Speed = 4.8f; Range = 1 |}

// mob animation frame coutn
let getMobFrameCount = function
    | Idle -> 8
    | Attacking -> 4
    | Hurting -> 6

// tower frame count
let getTowerFrameCount = function
    | Basic -> 5
    | Rapid -> 5
    | Area -> 6

// tower speed rate
let getTowerFrameDuration = function
    | Basic -> 0.08f
    | Rapid -> 0.05f
    | Area -> 0.08f

//cellcenter
let cellCenter row col =
    Vector2(
        float32 (MARGIN_X + col * CELL_SIZE + (CELL_SIZE / 2)),
        float32 (MARGIN_Y + row * CELL_SIZE + (CELL_SIZE / 2))
    )

//cellcenter X
let cellCenterX col =
    float32 (MARGIN_X + col * CELL_SIZE + (CELL_SIZE / 2))

// monster center
let mobCenter (m: Mob) =
    Vector2(m.VisualX, float32 (MARGIN_Y + m.Row * CELL_SIZE + (CELL_SIZE / 2)))

// col range
let clampCol col =
    max 0 (min (COLS - 1) col)

// visual col from x 
let visualColFromX (x: float32) =
    let raw = int (Math.Floor(float (x - float32 MARGIN_X) / float CELL_SIZE))
    clampCol raw


let mobVisualCol (m: Mob) =
    visualColFromX m.VisualX

let syncMobCellFromVisual (m: Mob) =
    m.Col <- mobVisualCol m

// tower sprite area
let towerSourceRect towerType frame =
    let tex = getTowerTexture towerType
    let frameCount = getTowerFrameCount towerType
    let frameWidth = float32 tex.Width / float32 frameCount
    Rectangle(float32 frame * frameWidth, 0.0f, frameWidth, float32 tex.Height)

// bullet sprtie area
let bulletSourceRect = function
    | Basic -> Rectangle(38.0f, 230.0f, 125.0f, 56.0f)
    | Rapid -> Rectangle(200.0f, 235.0f, 118.0f, 54.0f)
    | Area -> Rectangle(372.0f, 208.0f, 90.0f, 95.0f)

// bullet size
let bulletSize = function
    | Basic -> Vector2(56.0f, 26.0f)
    | Rapid -> Vector2(50.0f, 24.0f)
    | Area -> Vector2(132.0f, 132.0f)

let healthBarBorderColor = Color(24, 86, 36, 170)
let healthBarBackgroundColor = Color(80, 132, 62, 190)
let healthBarFillColor = Color(72, 226, 88, 255)

let healthRatio current maxHp =
    if maxHp <= 0 then
        0.0f
    else
        max 0.0f (min 1.0f (float32 current / float32 maxHp))

// ============================================================
//  4. game state
// ============================================================
// game state
type GameState = {
    mutable Towers: Tower list
    mutable Mobs: Mob list  
    mutable Projectiles: Projectile list
    mutable MobProjectiles: MobProjectile list
    mutable CastleHP: int
    mutable Money: int
    mutable Status: GameStatus
    mutable Selected: TowerType
    mutable Wave: int
    mutable MobsToSpawn: int
    mutable SpawnTimer: float32
    mutable WaveDelayTimer: float32
    mutable IncomeTimer: float32
    mutable Errormessage: string
    mutable ErrorTimer: float32
}

// initgame
let initGame () = {
    Towers = []
    Mobs = []
    Projectiles = []
    MobProjectiles = []
    CastleHP = 1000
    Money = 200
    Status = Playing
    Selected = Basic
    Wave = 1
    MobsToSpawn = INITIAL_MOBS_PER_WAVE
    SpawnTimer = 0.0f
    WaveDelayTimer = 0.0f
    IncomeTimer = 0.0f
    Errormessage = ""
    ErrorTimer = 0.0f
}

// mob find
let mobsInCell (gs: GameState) row col =
    gs.Mobs
    |> List.filter (fun m -> m.HP > 0 && m.Row = row && mobVisualCol m = col)

// find in area
let mobsInArea (gs: GameState) row col =
    gs.Mobs
    |> List.filter (fun m -> m.HP > 0 && abs(m.Row - row) <= 1 && abs(mobVisualCol m - col) <= 1)


let findMobTargetTower (gs: GameState) row col range =
    gs.Towers
    |> List.filter (fun t -> t.Row = row && t.Col < col && (col - t.Col) <= range)
    |> List.sortBy (fun t -> t.Col)
    |> List.tryHead

// ============================================================
//  5. attack process
// ============================================================
let spawnProjectile (gs: GameState) (t: Tower) targetRow targetCol targetIds =
    let stats = getTowerStats t.Type


    let projectile = {
        Pos = cellCenter t.Row t.Col
        TargetId = targetIds |> List.tryHead |> Option.defaultValue -1
        TargetIds = targetIds
        TargetRow = targetRow
        TargetCol = targetCol
        TargetPos = cellCenter targetRow targetCol
        Type = t.Type
        Damage = stats.ATK
        Speed = if t.Type = Rapid then 760.0f else 560.0f
        IsArea = (t.Type = Area)
    }
    gs.Projectiles <- projectile :: gs.Projectiles

let spawnMobProjectile (gs: GameState) (m: Mob) damage targetRow targetCol isCastleTarget =
    let targetPos =
        if isCastleTarget then
            Vector2(float32 (MARGIN_X - 45), float32 (MARGIN_Y + targetRow * CELL_SIZE + (CELL_SIZE / 2)))
        else
            cellCenter targetRow targetCol

    let projectile = {
        Pos = mobCenter m
        TargetRow = targetRow
        TargetCol = targetCol
        TargetPos = targetPos
        Damage = damage
        Speed = 430.0f
        IsCastleTarget = isCastleTarget
    }
    gs.MobProjectiles <- projectile :: gs.MobProjectiles


let updateProjectiles (gs: GameState) (dt: float32) =
    gs.Projectiles <-
        gs.Projectiles
        |> List.choose (fun p ->
            let visualTargetPos =
                gs.Mobs
                |> List.tryFind (fun m -> m.Id = p.TargetId && m.HP > 0)
                |> Option.map mobCenter
                |> Option.defaultValue p.TargetPos

            let toTarget = visualTargetPos - p.Pos
            let distance = toTarget.Length()
            let step = p.Speed * dt

            if distance <= step || distance < 10.0f then
                p.Pos <- visualTargetPos
                let hitRow, hitCol =
                    gs.Mobs
                    |> List.tryFind (fun m -> m.Id = p.TargetId && m.HP > 0)
                    |> Option.map (fun m -> m.Row, mobVisualCol m)
                    |> Option.defaultValue (p.TargetRow, p.TargetCol)

                let hitTargets =
                    if p.IsArea then
                        mobsInArea gs hitRow hitCol
                    else
                        mobsInCell gs hitRow hitCol

                hitTargets
                |> List.iter (fun m ->
                    m.HP <- m.HP - p.Damage
                    m.Action <- Hurting
                    m.CurrentFrame <- 0
                    m.FrameTimer <- 0.0f)
                None
            else
                if distance > 0.0f then
                    p.Pos <- p.Pos + Vector2.Normalize(toTarget) * step
                else
                    p.Pos <- visualTargetPos
                Some p)

let updateMobProjectiles (gs: GameState) (dt: float32) =
    gs.MobProjectiles <-
        gs.MobProjectiles
        |> List.choose (fun p ->
            let toTarget = p.TargetPos - p.Pos
            let distance = toTarget.Length()
            let step = p.Speed * dt

            if distance <= step || distance < 10.0f then
                p.Pos <- p.TargetPos
                if p.IsCastleTarget then
                    gs.CastleHP <- gs.CastleHP - p.Damage
                else
                    gs.Towers
                    |> List.tryFind (fun t -> t.Row = p.TargetRow && t.Col = p.TargetCol && t.HP > 0)
                    |> Option.iter (fun t -> t.HP <- t.HP - p.Damage)
                None
            else
                if distance > 0.0f then
                    p.Pos <- p.Pos + Vector2.Normalize(toTarget) * step
                else
                    p.Pos <- p.TargetPos
                Some p)


let updateTowerAnimation (t: Tower) dt =
    if t.IsAttacking then
        t.AnimTimer <- t.AnimTimer + dt
        if t.AnimTimer >= getTowerFrameDuration t.Type then
            t.AnimTimer <- 0.0f
            t.AnimFrame <- t.AnimFrame + 1
            if t.AnimFrame >= getTowerFrameCount t.Type then
                t.AnimFrame <- 0
                t.IsAttacking <- false

// tower attack process
let updateTowers (gs: GameState) dt =
    for t in gs.Towers do
        updateTowerAnimation t dt

        t.AttackTimer <- t.AttackTimer + dt
        let stats = getTowerStats t.Type

        if t.AttackTimer >= stats.Cooldown then
            let targetMobOpt =
                gs.Mobs
                |> List.filter (fun m -> m.Row = t.Row && m.HP > 0)
                |> List.sortBy (fun m -> m.VisualX)
                |> List.tryHead

            match targetMobOpt with
            | Some targetMob ->
                let targetCol = mobVisualCol targetMob
                let targetIds =
                    if t.Type = Area then
                        [ targetMob.Id ]
                    else
                        mobsInCell gs targetMob.Row targetCol
                        |> List.map (fun m -> m.Id)

                t.AttackTimer <- 0.0f
                t.AnimFrame <- 0
                t.AnimTimer <- 0.0f
                t.IsAttacking <- true
                spawnProjectile gs t targetMob.Row targetCol targetIds
            | None -> ()

// mob update
let updateMobAnimation (m: Mob) dt =
    let frameSpeed = 0.12f
    let maxFrames = getMobFrameCount m.Action

    m.FrameTimer <- m.FrameTimer + dt
    if m.FrameTimer >= frameSpeed then
        m.FrameTimer <- 0.0f
        m.CurrentFrame <- m.CurrentFrame + 1
        if m.CurrentFrame >= maxFrames then
            m.CurrentFrame <- 0
            if m.Action <> Idle then
                m.Action <- Idle

// mob move , attack process
let updateMobs (gs: GameState) dt =
    for m in gs.Mobs do
        let stats = getMobStats m.Type
        updateMobAnimation m dt

        syncMobCellFromVisual m
        let targetTower = findMobTargetTower gs m.Row m.Col stats.Range
        let castleInRange = m.Col < stats.Range

        if targetTower.IsSome || castleInRange then
            m.MoveTimer <- 0.0f
            // 공격 시작 위치: 사거리 진입 시 현재 셀 중앙에 정지
            m.VisualX <- cellCenterX m.Col

            if m.Action = Idle then
                m.Action <- Attacking
                m.CurrentFrame <- 0

            m.AttackTimer <- m.AttackTimer + dt
            if m.AttackTimer >= 1.0f then
                m.AttackTimer <- 0.0f
                if castleInRange then
                    if m.Type = RangedMob then
                        spawnMobProjectile gs m stats.ATK m.Row 0 true
                    else
                        gs.CastleHP <- gs.CastleHP - stats.ATK
                else
                    match targetTower with
                    | Some t ->
                        if m.Type = RangedMob then
                            spawnMobProjectile gs m stats.ATK t.Row t.Col false
                        else
                            t.HP <- t.HP - stats.ATK
                    | None -> ()
        else
            if m.Action = Attacking then
                m.Action <- Idle

            let pixelsPerSecond = float32 CELL_SIZE / stats.Speed
            m.VisualX <- m.VisualX - pixelsPerSecond * dt
            syncMobCellFromVisual m

// ============================================================
//  6. Update
// ============================================================
// game logic/frame 
let updateGame (gs: GameState) (dt: float32) (rand: Random) (nextMobId: byref<int>) =
    gs.IncomeTimer <- gs.IncomeTimer + dt
    if gs.IncomeTimer >= 1.0f then
        gs.Money <- gs.Money + 1
        gs.IncomeTimer <- 0.0f

    if gs.MobsToSpawn > 0 then
        let mutable wavebase = 2.0f - (float32 (gs.Wave - 1) * 0.08f)
        if wavebase < 0.4f then wavebase <- 0.4f
        let randomoffset = float32 (rand.NextDouble() * 1.0 - 1.0)
        wavebase <- wavebase + randomoffset
        if wavebase < 0.2f then wavebase <- 0.2f

        gs.SpawnTimer <- gs.SpawnTimer + dt
        if gs.SpawnTimer >= wavebase then
            let row = rand.Next(0, ROWS)
            let chance = rand.Next(100)
            let mobType = if chance < 20 then TankerMob elif chance < 50 then RangedMob else BasicMob
            let stats = getMobStats mobType
            let startX = cellCenterX (COLS - 1)
            let mob = {
                Id = nextMobId
                Type = mobType
                HP = stats.HP
                MaxHP = stats.HP
                Row = row
                Col = COLS - 1
                VisualX = startX
                AttackTimer = 0.0f
                MoveTimer = 0.0f
                Action = Idle
                CurrentFrame = 0
                FrameTimer = 0.0f
            }
            gs.Mobs <- mob :: gs.Mobs
            nextMobId <- nextMobId + 1
            gs.MobsToSpawn <- gs.MobsToSpawn - 1
            gs.SpawnTimer <- 0.0f
    
    elif List.isEmpty gs.Mobs then
        gs.WaveDelayTimer <- gs.WaveDelayTimer + dt
        if gs.WaveDelayTimer >= 3.0f then
            gs.Wave <- gs.Wave + 1
            gs.MobsToSpawn <- INITIAL_MOBS_PER_WAVE + (gs.Wave - 1) * MOBS_PER_WAVE_INCREASE
            gs.WaveDelayTimer <- 0.0f

    updateProjectiles gs dt
    updateMobProjectiles gs dt
    updateTowers gs dt
    updateMobs gs dt

    if gs.ErrorTimer > 0.0f then
        gs.ErrorTimer <- gs.ErrorTimer - dt

// ============================================================
//  7. Render
// ============================================================
// tower draw
let drawTower (t: Tower) =
    let tx = float32 (MARGIN_X + t.Col * CELL_SIZE)
    let ty = float32 (MARGIN_Y + t.Row * CELL_SIZE)
    let currentTex = getTowerTexture t.Type

    Raylib.DrawCircle(int tx + 45, int ty + 80, 20.0f, Color(0, 0, 0, 50))

    let destRec = Rectangle(tx + 5.0f, ty + 5.0f, float32 CELL_SIZE - 10.0f, float32 CELL_SIZE - 10.0f)
    if textureLoaded currentTex then
        Raylib.DrawTexturePro(currentTex, towerSourceRect t.Type t.AnimFrame, destRec, Vector2.Zero, 0.0f, Color.White)

// tower health bar draw
let drawTowerHealthBar (t: Tower) =
    let tx = float32 (MARGIN_X + t.Col * CELL_SIZE)
    let ty = float32 (MARGIN_Y + t.Row * CELL_SIZE)
    let barWidth = CELL_SIZE - 20
    let hpW = int (float32 barWidth * healthRatio t.HP t.MaxHP)
    let barX = int tx + 10
    let barY = int ty - 10
    Raylib.DrawRectangle(barX - 1, barY - 1, barWidth + 2, 7, healthBarBorderColor)
    Raylib.DrawRectangle(barX, barY, barWidth, 5, healthBarBackgroundColor)
    Raylib.DrawRectangle(barX, barY, hpW, 5, healthBarFillColor)

// projectile draw
let drawProjectile (p: Projectile) =
    if textureLoaded texBullets then
        let sourceRec = bulletSourceRect p.Type
        let size = bulletSize p.Type
        let destRec = Rectangle(p.Pos.X, p.Pos.Y, size.X, size.Y)
        let origin = Vector2(size.X / 2.0f, size.Y / 2.0f)
        Raylib.DrawTexturePro(texBullets, sourceRec, destRec, origin, 0.0f, Color.White)
    else
        if p.IsArea then
            Raylib.DrawCircleV(p.Pos, 42.0f, Color(173, 116, 255, 70))
            Raylib.DrawCircleLines(int p.Pos.X, int p.Pos.Y, 58.0f, Color(116, 64, 202, 70))

let drawMobProjectile (p: MobProjectile) =
    Raylib.DrawCircleV(Vector2(p.Pos.X + 5.0f, p.Pos.Y + 2.0f), 5.0f, Color(53, 151, 197, 150))
    Raylib.DrawCircleV(p.Pos, 9.0f, Color(99, 211, 238, 245))
    Raylib.DrawCircleV(Vector2(p.Pos.X - 3.0f, p.Pos.Y - 4.0f), 4.0f, Color(188, 245, 255, 235))
    Raylib.DrawCircleLines(int p.Pos.X, int p.Pos.Y, 9.0f, Color(30, 112, 164, 220))

// mob draw
let drawMob (m: Mob) =
    let tex = getMobCurrentTexture m.Type m.Action
    let cols = getMobFrameCount m.Action
    let centerY = float32 (MARGIN_Y + m.Row * CELL_SIZE + (CELL_SIZE / 2)) - 6.0f

    let targetHeight =
        match m.Type with
        | TankerMob -> 80.0f
        | RangedMob -> 64.0f
        | BasicMob -> 72.0f

    if textureLoaded tex then
        let fw = float32 (tex.Width / cols)
        let fh = float32 tex.Height
        let sourceRec = Rectangle(float32 m.CurrentFrame * fw, 0.0f, fw, fh)
        let scale = if fh > 0.0f then targetHeight / fh else 1.0f
        let destRec = Rectangle(m.VisualX, centerY, fw * scale, fh * scale)
        let origin = Vector2((fw * scale) / 2.0f, (fh * scale) / 2.0f)
        Raylib.DrawTexturePro(tex, sourceRec, destRec, origin, 0.0f, Color.White)

    let hpBarY = int (centerY - (targetHeight / 2.0f) - 8.0f)
    Raylib.DrawRectangle(int m.VisualX - 20, hpBarY, 40, 4, Color.Black)
    Raylib.DrawRectangle(int m.VisualX - 20, hpBarY, int (40.0f * healthRatio m.HP m.MaxHP), 4, Color.Red)

// ============================================================
//  8. main loop
// ============================================================
type GameResult = BackToMenu | Restart | Quit

let runGame () : GameResult =
    Raylib.SetTargetFPS(60)

    // load
    bgTexture <- Raylib.LoadTexture("background/grass.png")
    spriteSheet <- Raylib.LoadTexture("resources/units.png")
    texBasicTower <- loadTextureWithWhiteTransparent("tower/basic.png")
    texRapidTower <- loadTextureWithWhiteTransparent("tower/speed.png")
    texAreaTower <- loadTextureWithWhiteTransparent("tower/range.png")
    texBullets <- Raylib.LoadTexture("tower/bullets.png")
    uiFont <- Raylib.LoadFontEx("fonts/LuckiestGuy-Regular.ttf", 48, null, 0)
    if Raylib.IsFontValid(uiFont) = true then
        Raylib.SetTextureFilter(uiFont.Texture, TextureFilter.Bilinear)
    texBasic <- { Idle = Raylib.LoadTexture("Green_Slime/Idle.png"); Atk = Raylib.LoadTexture("Green_Slime/Attack_1.png"); Hurt = Raylib.LoadTexture("Green_Slime/Hurt.png") }
    texRanged <- { Idle = Raylib.LoadTexture("Blue_Slime/Idle.png"); Atk = Raylib.LoadTexture("Blue_Slime/Attack_1.png"); Hurt = Raylib.LoadTexture("Blue_Slime/Hurt.png") }
    texTanker <- { Idle = Raylib.LoadTexture("Red_Slime/Idle.png"); Atk = Raylib.LoadTexture("Red_Slime/Attack_1.png"); Hurt = Raylib.LoadTexture("Red_Slime/Hurt.png") }
    texCastle <- Raylib.LoadTexture("tower/boss_tower.png")

    // init
    let mutable gs = initGame()
    let rand = Random()
    let mutable nextId = 0
    let mutable gameResult = Quit

    // frame lop    
    while Raylib.WindowShouldClose() = false && gameResult = Quit do
        let dt = Raylib.GetFrameTime()

        if gs.Status = Playing then
            // tower select input
            if Raylib.IsKeyPressed(KeyboardKey.One) = true then gs.Selected <- Basic
            if Raylib.IsKeyPressed(KeyboardKey.Two) = true then gs.Selected <- Rapid
            if Raylib.IsKeyPressed(KeyboardKey.Three) = true then gs.Selected <- Area

            // tower install input
            if Raylib.IsMouseButtonPressed(MouseButton.Left) = true then
                let mousePos = Raylib.GetMousePosition()
                let colIdx = int ((mousePos.X - float32 MARGIN_X) / float32 CELL_SIZE)
                let rowIdx = int ((mousePos.Y - float32 MARGIN_Y) / float32 CELL_SIZE)

                if rowIdx >= 0 && rowIdx < ROWS && colIdx >= 0 && colIdx < COLS - 1 then
                    let stats = getTowerStats gs.Selected
                    let occupiedByTower = gs.Towers |> List.exists (fun t -> t.Row = rowIdx && t.Col = colIdx)

                    if occupiedByTower then
                        gs.Errormessage <- "ALREADY OCCUPIED"
                        gs.ErrorTimer <- 2.0f
                    elif gs.Money < stats.Cost then
                        gs.Errormessage <- "NOT ENOUGH MONEY"
                        gs.ErrorTimer <- 2.0f
                    else
                        let tower = {
                            Type = gs.Selected
                            HP = stats.HP
                            MaxHP = stats.HP
                            Row = rowIdx
                            Col = colIdx
                            AttackTimer = 0.0f
                            AnimFrame = 0
                            AnimTimer = 0.0f
                            IsAttacking = false
                        }
                        gs.Towers <- tower :: gs.Towers
                        gs.Money <- gs.Money - stats.Cost
                else
                    gs.Errormessage <- "SELECT A BUILDABLE CELL"
                    gs.ErrorTimer <- 2.0f

            updateGame gs dt rand &nextId

            if gs.CastleHP <= 0 then gs.Status <- GameOver
            gs.Mobs |> List.filter (fun m -> m.HP <= 0) |> List.iter (fun m -> gs.Money <- gs.Money + (getMobStats m.Type).Reward)
            gs.Mobs <- gs.Mobs |> List.filter (fun m -> m.HP > 0)
            gs.Towers <- gs.Towers |> List.filter (fun t -> t.HP > 0)

        // render
        Raylib.BeginDrawing()
        Raylib.ClearBackground(Color(132, 190, 70, 255))

        if textureLoaded bgTexture then
            let srcRec = Rectangle(0.0f, 0.0f, float32 bgTexture.Width, float32 bgTexture.Height)
            let destRec = Rectangle(0.0f, 0.0f, 1200.0f, 750.0f)
            Raylib.DrawTexturePro(bgTexture, srcRec, destRec, Vector2.Zero, 0.0f, Color.White)

        // grid
        for r in 0 .. ROWS - 1 do
            for c in 0 .. COLS - 1 do
                let posX = MARGIN_X + c * CELL_SIZE
                let posY = MARGIN_Y + r * CELL_SIZE
                let bgColor =
                    if (r + c) % 2 = 0 then Color(174, 220, 105, 78)
                    else Color(122, 184, 83, 70)
                Raylib.DrawRectangle(posX, posY, CELL_SIZE, CELL_SIZE, bgColor)
                Raylib.DrawRectangleLines(posX, posY, CELL_SIZE, CELL_SIZE, Color(59, 103, 48, 130))

        // ---------------------------------------------------------
        // hp bar, castle draw
        // ---------------------------------------------------------
        let castleW = 160.0f
        let castleH = 200.0f
        // 성의 위치: 그리드(MARGIN_X)의 바로 왼쪽, 5행의 수직 중앙에 배치
        let castleX = float32 (MARGIN_X - int castleW-10) 
        let castleY = float32 (MARGIN_Y + (ROWS * CELL_SIZE) / 2 - int castleH / 2)


        if textureLoaded texCastle then
            let srcRec = Rectangle(0.0f, 0.0f, float32 texCastle.Width, float32 texCastle.Height)
            let destRec = Rectangle(castleX, castleY, castleW, castleH)
            
            Raylib.DrawTexturePro(texCastle, srcRec, destRec, Vector2.Zero, 0.0f, Color.White)
        else
            Raylib.DrawRectangle(int castleX, int castleY, int castleW, int castleH, Color.Brown)

        let barW = 100.0f
        let barH = 12.0f
        let barX = castleX + (castleW - barW) / 2.0f
        let barY = castleY - 25.0f

        Raylib.DrawRectangle(int barX - 2, int barY - 2, int barW + 4, int barH + 4, healthBarBorderColor)
        Raylib.DrawRectangle(int barX, int barY, int barW, int barH, healthBarBackgroundColor)
        let hpRatio = max 0.0f (min 1.0f (float32 gs.CastleHP / CASTLE_MAX_HP))
        let currentW = int (barW * hpRatio)
        Raylib.DrawRectangle(int barX, int barY, currentW, int barH, healthBarFillColor)
        
        drawUiText (sprintf "%d / %d" gs.CastleHP (int CASTLE_MAX_HP)) (int barX) (int barY - 18) 17.0f 1.0f Color.DarkGray

        for t in gs.Towers do drawTower t
        for p in gs.Projectiles do drawProjectile p
        for p in gs.MobProjectiles do drawMobProjectile p
        for m in gs.Mobs do drawMob m
        for t in gs.Towers do drawTowerHealthBar t

        // UI
        Raylib.DrawRectangle(0, 0, 1200, UI_HEIGHT, Color(48, 86, 42, 238))
        Raylib.DrawRectangle(0, UI_HEIGHT - 12, 1200, 12, Color(102, 76, 38, 255))
        Raylib.DrawRectangle(0, UI_HEIGHT - 4, 1200, 4, Color(223, 184, 86, 255))

        let panelFill = Color(246, 231, 164, 226)
        let panelEdge = Color(86, 64, 36, 255)
        let labelColor = Color(82, 103, 47, 255)
        let valueColor = Color(53, 55, 31, 255)

        let drawInfoPanel x w label value =
            Raylib.DrawRectangle(x, 16, w, 72, panelFill)
            Raylib.DrawRectangle(x, 16, w, 7, Color(231, 196, 96, 230))
            Raylib.DrawRectangleLines(x, 16, w, 72, panelEdge)
            drawUiText label (x + 14) 23 17.0f 1.0f labelColor
            drawUiText value (x + 14) 48 30.0f 1.0f valueColor

        drawInfoPanel 24 220 "MONEY" (sprintf "%d" gs.Money)
        drawInfoPanel 260 170 "WAVE" (sprintf "%d" gs.Wave)
        drawInfoPanel 446 270 "CASTLE HP" (sprintf "%d" (max 0 gs.CastleHP))

        Raylib.DrawRectangle(732, 16, 428, 72, panelFill)
        Raylib.DrawRectangle(732, 16, 428, 7, Color(231, 196, 96, 230))
        Raylib.DrawRectangleLines(732, 16, 428, 72, panelEdge)
        drawUiText "SELECTED" 752 23 17.0f 1.0f labelColor
        drawUiText (sprintf "%A" gs.Selected) 752 48 30.0f 1.0f valueColor
        drawUiText (sprintf "COST %d" (getTowerStats gs.Selected).Cost) 910 56 18.0f 1.0f labelColor

        let selectedTex = getTowerTexture gs.Selected
        if textureLoaded selectedTex then
            Raylib.DrawTexturePro(selectedTex, towerSourceRect gs.Selected 0, Rectangle(1076.0f, 17.0f, 70.0f, 70.0f), Vector2.Zero, 0.0f, Color.White)

        if gs.ErrorTimer > 0.0f then
            drawUiText gs.Errormessage 430 (UI_HEIGHT + 8) 24.0f 1.0f Color.Red

        if gs.Status = GameOver then
            let finalWave = max 0 (gs.Wave - 1)
            Raylib.DrawRectangle(0, 0, 1200, 750, Color(0, 0, 0, 180))
            drawUiText "GAME OVER" 390 280 76.0f 1.0f Color.Red
            drawUiText (sprintf "FINAL WAVE: %d" finalWave) 430 380 48.0f 1.0f Color.White
            
            let restartBtn = Rectangle(285.0f, 520.0f, 240.0f, 70.0f)
            let menuBtn = Rectangle(645.0f, 520.0f, 240.0f, 70.0f)
            let mousePos = Raylib.GetMousePosition()
            let hoverRestart = Raylib.CheckCollisionPointRec(mousePos, restartBtn) = CBool true
            let hoverMenu = Raylib.CheckCollisionPointRec(mousePos, menuBtn) = CBool true
            let click = Raylib.IsMouseButtonPressed(MouseButton.Left) = CBool true
            
            let drawGameOverBtn (rect: Rectangle) (label: string) (hovered: bool) =
                let bg = if hovered then Color(160, 220, 100, 240) else Color(140, 200, 80, 220)
                Raylib.DrawRectangleRec(rect, bg)
                Raylib.DrawRectangleLinesEx(rect, 3.0f, Color(180, 230, 140, 255))
                let textSize : Vector2 = Raylib.MeasureTextEx(uiFont, label, 28.0f, 1.0f)
                let textX = rect.X + (rect.Width - textSize.X) / 2.0f
                let textY = rect.Y + (rect.Height - textSize.Y) / 2.0f
                Raylib.DrawTextEx(uiFont, label, Vector2(textX, textY), 28.0f, 1.0f, Color.White)
            
            drawGameOverBtn restartBtn "RESTART" hoverRestart
            drawGameOverBtn menuBtn "GO TO MENU" hoverMenu
            
            if click then
                if hoverRestart then
                    gameResult <- Restart
                elif hoverMenu then
                    gameResult <- BackToMenu

        Raylib.EndDrawing()

    // asset unload
    unloadIfLoaded bgTexture
    unloadIfLoaded spriteSheet
    unloadIfLoaded texBasicTower
    unloadIfLoaded texRapidTower
    unloadIfLoaded texAreaTower
    unloadIfLoaded texBullets
    if Raylib.IsFontValid(uiFont) = CBool true then
        Raylib.UnloadFont(uiFont)
    unloadMobTextures texBasic
    unloadMobTextures texRanged
    unloadMobTextures texTanker

    if Raylib.WindowShouldClose() = CBool true then Quit else gameResult

