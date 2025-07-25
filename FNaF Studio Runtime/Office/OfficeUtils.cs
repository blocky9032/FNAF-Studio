﻿using FNaFStudio_Runtime.Data;
using FNaFStudio_Runtime.Data.Definitions;
using FNaFStudio_Runtime.Data.Definitions.GameObjects;
using FNaFStudio_Runtime.Util;
using Raylib_CsLo;
using System.Numerics;

namespace FNaFStudio_Runtime.Office;

public class OfficeUtils
{
    private static void Toggle(ref bool state)
    {
        state = !state;
    }

    public static Button2D GetButtonWithCallback(string id, GameJson.OfficeObject obj, Action<Button2D>? setup = null)
    {
        if (!GameCache.Buttons.TryGetValue(id, out var button) && obj.Position != null &&
            !string.IsNullOrEmpty(obj.Sprite))
        {
            var tex = Cache.GetTexture(obj.Sprite);
            button = new Button2D(new Vector2(obj.Position[0] * Globals.xMagic, obj.Position[1] * Globals.yMagic), obj,
                texture: tex);
            setup?.Invoke(button);
            GameCache.Buttons[id] = button;
        }

        return button!;
    }

    public static Button2D GetDoorButton(string id, GameJson.OfficeObject obj)
    {
        return GetButtonWithCallback(id, obj, button =>
        {
            if (OfficeCore.OfficeState != null && obj.ID != null)
            {
                var doorVars = OfficeCore.OfficeState.Office.Doors[obj.ID];
                button.OnClick(() =>
                {
                    if (doorVars.Animation != null && !doorVars.Animation.Current().HasFramesLeft())
                    {
                        Toggle(ref doorVars.IsClosed);
                        Toggle(ref doorVars.Button.IsOn);

                        if (doorVars.IsClosed)
                        {
                            SoundPlayer.PlayOnChannel(doorVars.CloseSound, false, 13);
                            OfficeCore.OfficeState.Power.Usage += 1;
                        }
                        else
                        {
                            SoundPlayer.PlayOnChannel(doorVars.OpenSound, false, 13);
                            OfficeCore.OfficeState.Power.Usage -= 1;
                        }
                        doorVars.Animation.Reverse();
                    }
                });
            }
        });
    }

    public static Button2D GetLightButton(string id, GameJson.OfficeObject obj)
    {
        if (OfficeCore.OfficeState == null || obj.ID == null)
            return GetButtonWithCallback(id, obj, _ => { });

        return GetButtonWithCallback(id, obj, button =>
        {
            void HandleToggle()
            {
                if (OfficeCore.OfficeState != null)
                {
                    var stateParts = OfficeCore.OfficeState.Office.State.Split(':');
                    var isLightOn = OfficeCore.OfficeState.Office.Lights[obj.ID].IsOn;
                    if (stateParts.Length == 2 && !isLightOn)
                    {
                        Toggle(ref OfficeCore.OfficeState.Office.Lights[stateParts[0]].IsOn);
                        OfficeCore.OfficeState.Power.Usage -= 1;

                        OfficeCore.OfficeState.Office.State = $"{obj.ID}:{(stateParts[1]
                                .Split(',')
                                .Skip(1)
                                .DefaultIfEmpty("Default")
                                .Aggregate((acc, next) => acc + "," + next)
                            )}";

                        PathFinder.OnLightTurnedOff(stateParts[1].Split(',').First());
                    }
                    else
                    {
                        OfficeCore.OfficeState.Office.State = stateParts.Length == 2
                            ?
                            (stateParts[1]
                                .Split(',')
                                .Skip(1)
                                .DefaultIfEmpty("Default")
                                .Aggregate((acc, next) => acc + "," + next)
                            )
                            : $"{obj.ID}:{OfficeCore.OfficeState.Office.State}";
                    }

                    if (!isLightOn)
                    {
                        SoundPlayer.PlayOnChannel(obj.Sound, true, 12);
                        OfficeCore.OfficeState.Power.Usage += 1;

                        PathFinder.OnLightTurnedOn(obj.ID);
                    }
                    else
                    {
                        SoundPlayer.StopChannel(12);
                        OfficeCore.OfficeState.Power.Usage -= 1;

                        PathFinder.OnLightTurnedOff(stateParts[1].Split(',').First());

                    }

                    Toggle(ref OfficeCore.OfficeState.Office.Lights[obj.ID].IsOn);
                }
            }

            button.OnClick(HandleToggle);

            if (obj.Clickstyle)
                button.OnRelease(HandleToggle);
        });
    }

    private static void ToggleCams()
    {
        if (OfficeCore.OfficeState == null) return;

        SoundPlayer.SetChannelVolume(10, 0);
        (string, SceneType, int) checks = GameState.CurrentScene.Name == "CameraHandler" ?
        (GameState.Project.Sounds.Camdown, SceneType.Office, -1) : (GameState.Project.Sounds.Camup, SceneType.Cameras, 1);
        SoundPlayer.PlayOnChannel(checks.Item1, false, 2);
        RuntimeUtils.Scene.SetScenePreserve(checks.Item2);
        OfficeCore.OfficeState.Power.Usage += checks.Item3;
    }

    private static void ToggleMask()
    {
        if (OfficeCore.OfficeState == null) return;

        OfficeCore.OfficeState.Player.IsMaskOn = GameCache.HudCache.MaskAnim.State == AnimationState.Normal;
        string maskSound = OfficeCore.OfficeState.Player.IsMaskOn ?
            GameState.Project.Sounds.Maskoff : GameState.Project.Sounds.Maskon;
        SoundPlayer.PlayOnChannel(maskSound, false, 3);
        GameCache.HudCache.MaskAnim.Resume();
        GameCache.HudCache.MaskAnim.Show();
    }

    public static void ResetHUD()
    {
        GameCache.HudCache.Power = new("", 26, GameState.Project.Offices[OfficeCore.Office ?? "Office"].TextFont ?? "LCD Solid", Raylib.WHITE);
        GameCache.HudCache.Usage = new("", 26, GameState.Project.Offices[OfficeCore.Office ?? "Office"].TextFont ?? "LCD Solid", Raylib.WHITE);
        GameCache.HudCache.Time = new("", 26, GameState.Project.Offices[OfficeCore.Office ?? "Office"].TextFont ?? "LCD Solid", Raylib.WHITE);
        GameCache.HudCache.Night = new("", 22, GameState.Project.Offices[OfficeCore.Office ?? "Office"].TextFont ?? "LCD Solid", Raylib.WHITE);

        GameCache.HudCache.CameraAnim.Reset();
        GameCache.HudCache.MaskAnim.Reset();

        GameCache.HudCache.CameraAnim.OnPlay(ToggleCams, AnimationState.Reverse);
        GameCache.HudCache.CameraAnim.OnFinish(ToggleCams, AnimationState.Normal);
        GameCache.HudCache.CameraAnim.OnFinish(() =>
        {
            // this executes the first time we play an animation
            // for some reason and it causes a single frame of
            // office to appear when opening the camera panel
            GameCache.HudCache.CameraAnim.Hide();
            GameCache.HudCache.CameraAnim.Reverse();
        });

        GameCache.HudCache.MaskAnim.OnPlay(() => SoundPlayer.StopChannel(8), AnimationState.Reverse);
        GameCache.HudCache.MaskAnim.OnFinish(GameCache.HudCache.MaskAnim.Hide, AnimationState.Reverse);
        GameCache.HudCache.MaskAnim.OnFinish(GameCache.HudCache.MaskAnim.Reverse);
        GameCache.HudCache.MaskAnim.OnFinish(() =>
        {
            GameCache.HudCache.MaskAnim.Pause();
            SoundPlayer.PlayOnChannel(GameState.Project.Sounds.MaskBreathing, true, 8);
        }, AnimationState.Normal);

        GameCache.HudCache.CameraAnim.Hide();
        GameCache.HudCache.MaskAnim.Hide();
    }

    public static void DrawHUD()
    {
        if (OfficeCore.Office == null || OfficeCore.OfficeState == null)
        {
            Logger.LogWarnAsync("OfficeUtils: DrawHUD", "OfficeCore.Office/OfficeState is null!");
            return;
        }


        GameCache.HudCache.CameraAnim.AdvanceDraw(Vector2.Zero);
        GameCache.HudCache.MaskAnim.AdvanceDraw(Vector2.Zero);


        GameCache.HudCache.Power.Content = $"Power Left: {OfficeCore.OfficeState.Power.Level}%";
        GameCache.HudCache.Power.Draw(new(38, 601));

        GameCache.HudCache.Usage.Content = $"Usage: ";
        GameCache.HudCache.Usage.Draw(new(38, 637));
        Raylib.DrawTexture(Cache.GetTexture($"e.usage_{Math.Clamp(OfficeCore.OfficeState.Power.Usage, 0, 4) + 1}"), 136, 634, Raylib.WHITE);


        var minutes = TimeManager.GetTime().hours;
        GameCache.HudCache.Time.Content = $"{(minutes == 0 ? " 12" : minutes)} AM";
        GameCache.HudCache.Time.Draw(new(minutes == 0 ? 1160 : 1165, 10));

        GameCache.HudCache.Night.Content = $"Night {OfficeCore.OfficeState.Night}";
        GameCache.HudCache.Night.Draw(new(1160, 45));

        DrawUIButtons();

        if (OfficeCore.OfficeState.Settings.Toxic)
        {
            var player = OfficeCore.OfficeState.Player;
            player.ToxicLevel = Math.Clamp(player.ToxicLevel + (player.IsMaskOn ? 50 : -50) * Raylib.GetFrameTime(), 0, 280);

            if (player.IsMaskOn && player.ToxicLevel >= 280 && player.MaskEnabled)
            {
                player.MaskEnabled = false;
                ToggleMask();
                SoundPlayer.PlayOnChannel(GameState.Project.Sounds.MaskToxic, false, 3);
            }

            if (player.IsMaskOn || player.ToxicLevel > 0)
            {
                float toxicLevel = player.ToxicLevel / 280f;
                Color color = new((int)(toxicLevel * 255), (int)((1 - toxicLevel) * 255), 0, 255);
                Raylib.DrawTexture(Cache.GetTexture("e.toxic"), 25, 24, Raylib.WHITE);
                Raylib.DrawRectangle(30, 47, (int)Math.Clamp(toxicLevel * 114, 0, 114), 20, color);
            }

            player.MaskEnabled |= player.ToxicLevel <= 0;
        }

        if (GameState.DebugMode)
        {
            Raylib.DrawText("Time", 44 + 950, 88 - 88, 22, Raylib.WHITE);
            Raylib.DrawText("Seconds: " + TimeManager.GetTime().seconds, 88 + 950, 110 - 88, 22, Raylib.WHITE);
            Raylib.DrawText("Minutes: " + TimeManager.GetTime().minutes, 88 + 950, 132 - 88, 22, Raylib.WHITE);

            Raylib.DrawText("Animatronics", 44 + 950, 176 - 88, 22, Raylib.WHITE);
            var i = 0;
            var posY = 0;
            foreach (var anim in OfficeCore.OfficeState.Animatronics)
            {
                i++;
                posY = 176 + 22 * i;
                Raylib.DrawText(anim.Value.Name, 88 + 950, posY - 88, 22, Raylib.WHITE);
            }

            Raylib.DrawText("Cameras", 44 + 950, posY + 44 - 88, 22, Raylib.WHITE);
            var i2 = 0;
            foreach (var cam in OfficeCore.OfficeState.Cameras)
            {
                i2++;
                var camPosY = posY + 44 + 22 * i2;
                Raylib.DrawText(cam.Key, 88 + 950, camPosY - 88, 22, Raylib.WHITE);
            }
        }
    }

    public static void DrawUIButtons()
    {
        if (OfficeCore.OfficeState == null) return;

        foreach (var UIButton in OfficeCore.OfficeState.UIButtons)
        {
            if (UIButton.Value.Input?.Position == null) continue;

            Vector2 position = new((int)(UIButton.Value.Input.Position[0] * Globals.xMagic),
                (int)(UIButton.Value.Input.Position[1] * Globals.yMagic));

            if (!GameCache.Buttons.TryGetValue(UIButton.Key, out var button))
            {
                lock (GameState.buttonsLock)
                {
                    button = new Button2D(position, id: UIButton.Key, IsMovable: false,
                    texture: Cache.GetTexture(UIButton.Value.Input.Image)
                );

                    button.OnUnHover(() =>
                    {
                        if (button.ID == "camera")
                        {
                            OfficeCore.OfficeState.Player.IsCameraUp = GameCache.HudCache.CameraAnim.State == AnimationState.Normal;
                            GameCache.HudCache.CameraAnim.Show();
                        }
                        else if (button.ID == "mask")
                        {
                            ToggleMask();
                        }
                    });

                    GameCache.Buttons[UIButton.Key] = button;
                }
            }

            // This single expression reduced the total CPU time
            // by 1 ms (huge performance, atleast 100 FPS more)
            button.IsVisible = (UIButton.Key == "camera") ?
                (!OfficeCore.OfficeState.Player.IsMaskOn && OfficeCore.OfficeState.Player.CameraEnabled) :
                UIButton.Key != "mask" || (!OfficeCore.OfficeState.Player.IsCameraUp && OfficeCore.OfficeState.Player.MaskEnabled); ;
            button.Draw(position);
        }
    }
}