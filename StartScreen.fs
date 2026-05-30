#nowarn "3391"

module StartScreen

open System
open System.Numerics
open Raylib_cs
open GridSiege

[<EntryPoint>]
let main _ =
    let width, height = 1200, 750
    Raylib.InitWindow(width, height, "Grid Siege")
    Raylib.SetTargetFPS(60)

    let bgTexture = Raylib.LoadTexture("background/grass.png")
    let fontRaw = Raylib.LoadFontEx("fonts/LuckiestGuy-Regular.ttf", 72, null, 0)
    let titleFont = if Raylib.IsFontValid(fontRaw) = CBool true then fontRaw else Raylib.GetFontDefault()
    let unloadFont = Raylib.IsFontValid(fontRaw) = CBool true

    let startButton = Rectangle(450.0f, 380.0f, 300.0f, 70.0f)
    let exitButton = Rectangle(450.0f, 470.0f, 300.0f, 70.0f)
    
    let mutable showMenu = true
    let mutable countdownTimer = 0.0f
    let mutable showCountdown = false

    while showMenu && Raylib.WindowShouldClose() = false do
        let dt = Raylib.GetFrameTime()
        let mousePos = Raylib.GetMousePosition()
        let click = Raylib.IsMouseButtonPressed(MouseButton.Left) = CBool true
        let hoverStart = Raylib.CheckCollisionPointRec(mousePos, startButton) = CBool true
        let hoverExit = Raylib.CheckCollisionPointRec(mousePos, exitButton) = CBool true

        if not showCountdown then
            if click then
                if hoverStart then
                    showCountdown <- true
                    countdownTimer <- 3.0f
                elif hoverExit then
                    showMenu <- false
        else
            countdownTimer <- countdownTimer - dt
            if countdownTimer <= 0.0f then
                match runGame() with
                | BackToMenu ->
                    showCountdown <- false
                    countdownTimer <- 0.0f
                | Restart ->
                    showCountdown <- true
                    countdownTimer <- 3.0f
                | Quit ->
                    showMenu <- false

        Raylib.BeginDrawing()
        Raylib.ClearBackground(Color(132, 190, 70, 255))

        if bgTexture.Width > 0 && bgTexture.Height > 0 then
            let src = Rectangle(0.0f, 0.0f, float32 bgTexture.Width, float32 bgTexture.Height)
            let dest = Rectangle(0.0f, 0.0f, float32 width, float32 height)
            Raylib.DrawTexturePro(bgTexture, src, dest, Vector2.Zero, 0.0f, Color.White)

        if showCountdown then
            Raylib.DrawRectangle(0, 0, 1200, 750, Color(0, 0, 0, 180))
            let countNum = int (countdownTimer) + 1
            if countNum >= 1 && countNum <= 3 then
                let countText = sprintf "%d" countNum
                let countSize : Vector2 = Raylib.MeasureTextEx(titleFont, countText, 96.0f, 2.0f)
                let countX = (1200.0f - countSize.X) / 2.0f
                let countY = (750.0f - countSize.Y) / 2.0f 
                Raylib.DrawTextEx(titleFont, countText, Vector2(countX, countY), 96.0f, 2.0f, Color.Red)
            else
                let startText = "START!"
                let startSize : Vector2 = Raylib.MeasureTextEx(titleFont, startText, 96.0f, 2.0f)
                let startX = (1200.0f - startSize.X) / 2.0f
                let startY = (750.0f - startSize.Y) / 2.0f
                Raylib.DrawTextEx(titleFont, startText, Vector2(startX, startY), 96.0f, 2.0f, Color.Red)
        else
            let titleText = "Grid Siege"
            let titlePos = Vector2(120.0f, 110.0f)
            Raylib.DrawTextEx(titleFont, titleText, titlePos, 96.0f, 2.0f, Color(250, 230, 150, 255))
            Raylib.DrawTextEx(titleFont, "1, 2, 3 to select / click to install", Vector2(210.0f, 230.0f), 32.0f, 1.0f, Color(230, 230, 230, 255))

            let drawButton (rect: Rectangle) (label: string) (hovered: bool) =
                let bg = if hovered then Color(45, 135, 60, 220) else Color(30, 110, 45, 200)
                Raylib.DrawRectangleRec(rect, bg)
                Raylib.DrawRectangleLinesEx(rect, 4.0f, Color(255, 235, 160, 220))
                let textSize : Vector2 = Raylib.MeasureTextEx(titleFont, label, 32.0f, 2.0f)
                let textX = rect.X + (rect.Width - textSize.X) / 2.0f
                let textY = rect.Y + (rect.Height - textSize.Y) / 2.0f
                Raylib.DrawTextEx(titleFont, label, Vector2(textX, textY), 32.0f, 2.0f, Color.White)

            drawButton startButton "START" hoverStart
            drawButton exitButton "EXIT" hoverExit

        Raylib.EndDrawing()

    if unloadFont then
        Raylib.UnloadFont(fontRaw)
    if bgTexture.Width > 0 && bgTexture.Height > 0 then
        Raylib.UnloadTexture(bgTexture)

    Raylib.CloseWindow()
    0
