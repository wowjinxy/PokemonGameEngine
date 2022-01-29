﻿using Kermalis.PokemonGameEngine.Input;
using Kermalis.PokemonGameEngine.Render.OpenGL;
using SDL2;
using Silk.NET.OpenGL;
using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
#if DEBUG
using Kermalis.PokemonGameEngine.Debug;
#endif

namespace Kermalis.PokemonGameEngine.Render
{
    internal static class Display
    {
        private const string WINDOW_TITLE = "Pokémon Game Engine";
        private const string SCREENSHOT_PATH = @"Screenshots";
        private const int AUTOSIZE_WINDOW_SCALE = 3;
        private static readonly bool _debugScreenshotCurrentFrameBuffer = false;

        private static readonly IntPtr _window;
        private static readonly IntPtr _gl;

        public static readonly GL OpenGL;
        public static bool AutosizeWindow = true; // Works silently with fullscreen mode
        public static Vec2I ViewportSize;
        public static Vec2I ScreenSize;
        public static Rect ScreenRect;
        public static float DeltaTime;

        static Display()
        {
            // SDL 2
            if (SDL.SDL_Init(SDL.SDL_INIT_AUDIO | SDL.SDL_INIT_VIDEO | SDL.SDL_INIT_GAMECONTROLLER) != 0)
            {
                Print_SDL_Error("SDL could not initialize!");
            }

            // Use OpenGL 4.2 core. Required for glDrawArraysInstancedBaseInstance
            if (SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_MAJOR_VERSION, 4) != 0)
            {
                Print_SDL_Error("Could not set OpenGL's major version!");
            }
            if (SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_MINOR_VERSION, 2) != 0)
            {
                Print_SDL_Error("Could not set OpenGL's minor version!");
            }
            if (SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_PROFILE_MASK, SDL.SDL_GLprofile.SDL_GL_CONTEXT_PROFILE_CORE) != 0)
            {
                Print_SDL_Error("Could not set OpenGL's profile!");
            }

            SDL.SDL_WindowFlags windowFlags = SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL | SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE;
#if FULLSCREEN
            windowFlags |= SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN_DESKTOP;
#endif

            _window = SDL.SDL_CreateWindow(WINDOW_TITLE, SDL.SDL_WINDOWPOS_UNDEFINED, SDL.SDL_WINDOWPOS_UNDEFINED, 1, 1, windowFlags);
            if (_window == IntPtr.Zero)
            {
                Print_SDL_Error("Could not create the window!");
            }

            _gl = SDL.SDL_GL_CreateContext(_window);
            if (_gl == IntPtr.Zero)
            {
                Print_SDL_Error("Could not create the OpenGL context!");
            }
            if (SDL.SDL_GL_SetSwapInterval(1) != 0)
            {
                Print_SDL_Error("Could not enable VSync!");
            }
            if (SDL.SDL_GL_MakeCurrent(_window, _gl) != 0)
            {
                Print_SDL_Error("Could not start OpenGL on the window!");
            }
            OpenGL = GL.GetApi(SDL.SDL_GL_GetProcAddress);
            // Default gl states:
            // DepthTest disabled
            OpenGL.Enable(EnableCap.Blend); // Blend enabled
            OpenGL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
#if DEBUG
            unsafe
            {
                OpenGL.Enable(EnableCap.DebugOutput);
                OpenGL.DebugMessageCallback(HandleGLError, null);
            }
#endif

            SetMinimumWindowSize(new Vec2I(1, 1));
        }

        private static Vec2I GetWindowSize()
        {
            Vec2I ret;
            SDL.SDL_GetWindowSize(_window, out ret.X, out ret.Y);
            return ret;
        }
        public static void SetMinimumWindowSize(Vec2I size)
        {
            ScreenSize = size;
            SDL.SDL_SetWindowMinimumSize(_window, size.X, size.Y);
            if (AutosizeWindow)
            {
                size *= AUTOSIZE_WINDOW_SCALE;
                SDL.SDL_SetWindowSize(_window, size.X, size.Y);
            }
            SetScreenRect();
        }
        public static void Viewport(in Rect rect)
        {
            Vec2I size = rect.GetSize();
            OpenGL.Viewport(rect.TopLeft.X, rect.TopLeft.Y, (uint)size.X, (uint)size.Y);
            ViewportSize = size;
        }
        private static void SetScreenRect()
        {
            Vector2 windowSize = GetWindowSize();
            Vector2 ratios = windowSize / ScreenSize;
            float ratio = ratios.X < ratios.Y ? ratios.X : ratios.Y;
            Vector2 size = ScreenSize * ratio;
            Vector2 topLeft = (windowSize - size) * 0.5f;
            ScreenRect = Rect.FromSize((Vec2I)topLeft, (Vec2I)size);
        }

        /// <summary>Returns true if the current frame should be skipped</summary>
        public static bool PrepareFrame(ref DateTime mainLoopTime)
        {
            // Calculate delta time
            DateTime now = DateTime.Now;
            DateTime prev = mainLoopTime;
            mainLoopTime = now;
            if (now <= prev)
            {
#if DEBUG
                Log.WriteLineWithTime("Time went back!");
#endif
                DeltaTime = 0f;
                return true; // Skip current frame if time went back
            }
            else
            {
                DeltaTime = (float)(now - prev).TotalSeconds;
                if (DeltaTime > 1f)
                {
                    DeltaTime = 1f;
#if DEBUG
                    Log.WriteLineWithTime("Time between frames was longer than 1 second!");
#endif
                }
            }
            SetScreenRect();
            return false;
        }
        public static void PresentFrame()
        {
            if (InputManager.JustPressed(Key.Screenshot))
            {
                SaveScreenshot();
            }
            OpenGL.BindFramebuffer(FramebufferTarget.Framebuffer, 0); // Rebind default FBO. Streaming with many apps require this bound before swap
            SDL.SDL_GL_SwapWindow(_window);
        }

        private static void SaveScreenshot()
        {
            string path = Path.Combine(SCREENSHOT_PATH, string.Format("Screenshot_{0:MM-dd-yyyy_HH-mm-ss-fff}.png", DateTime.Now));
            if (_debugScreenshotCurrentFrameBuffer)
            {
                path = GLTextureUtils.SaveReadBufferAsImage(OpenGL, ViewportSize, path);
            }
            else
            {
                OpenGL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
                path = GLTextureUtils.SaveReadBufferAsImage(OpenGL, GetWindowSize(), path);
            }
#if DEBUG
            Log.WriteLineWithTime(string.Format("Screenshot saved to {0}", path));
#endif
        }

        public static void Print_SDL_Error(string error)
        {
            error = string.Format("{2}{0}SDL Error: \"{1}\"", Environment.NewLine, SDL.SDL_GetError(), error);
#if DEBUG
            Log.WriteLineWithTime(error);
#endif
            throw new Exception(error);
        }
#if DEBUG
        private static void HandleGLError(GLEnum _, GLEnum type, int id, GLEnum severity, int length, IntPtr message, IntPtr __)
        {
            if (severity == GLEnum.DebugSeverityNotification)
            {
                return;
            }
            // GL_INVALID_ENUM error generated. Operation is not valid from the core profile.
            if (id == 1280)
            {
                return; // Ignore legacy profile func warnings. I don't use any legacy functions, but streaming apps may attempt to when hooking in
            }
            // Pixel-path performance warning: Pixel transfer is synchronized with 3D rendering.
            if (id == 131154)
            {
                return; // Ignore NVIDIA driver warning. Happens when taking a screenshot with the entire screen
            }
            // Program/shader state performance warning: Vertex shader in program {num} is being recompiled based on GL state.
            if (id == 131218)
            {
                return; // Ignore NVIDIA driver warning. Not sure what causes it and neither is Google
            }
            string msg = Marshal.PtrToStringAnsi(message, length);
            Log.WriteLineWithTime("GL Error:");
            Log.ModifyIndent(+1);
            Log.WriteLine(string.Format("Message: \"{0}\"", msg));
            Log.WriteLine(string.Format("Type: \"{0}\"", type));
            Log.WriteLine(string.Format("Id: \"{0}\"", id));
            Log.WriteLine(string.Format("Severity: \"{0}\"", severity));
            Log.ModifyIndent(-1);
            ;
        }
#endif

        public static void Quit()
        {
            SDL.SDL_GL_DeleteContext(_gl);
            SDL.SDL_DestroyWindow(_window);
            SDL.SDL_Quit();
        }
    }
}