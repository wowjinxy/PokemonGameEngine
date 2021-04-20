﻿using Kermalis.PokemonGameEngine.Core;
using Kermalis.PokemonGameEngine.Util;
using Kermalis.PokemonGameEngine.World;
using SoLoud;
using System;
using System.Collections.Generic;
using System.IO;

namespace Kermalis.PokemonGameEngine.Sound
{
    internal static class SoundUtils
    {
        private sealed class Sound
        {
            public readonly Song Song;
            public readonly WavStream Wav;
            public uint Handle;

            public Sound(Song song, WavStream wav)
            {
                Song = song;
                Wav = wav;
            }
        }

        private static readonly Dictionary<Song, (string resource, double loopPoint)> _songResources = new Dictionary<Song, (string, double)>
        {
            { Song.Town1, ("Sound.BGM.Town1.ogg", 0.48) },
            { Song.Route1, ("Sound.BGM.Town1.ogg", 0.48) },//, ("Sound.BGM.Route1.ogg", 2.1818) },
            { Song.Cave1, ("Sound.BGM.Cave1.ogg", 3.75) },
            { Song.WildBattle, ("Sound.BGM.Town1.ogg", 0.48) },//, ("Sound.BGM.WildBattle.ogg", 0) },
            { Song.LegendaryBattle, ("Sound.BGM.Town1.ogg", 0.48) },//, ("Sound.BGM.LegendaryBattle.ogg", 0) },
        };

        private static readonly Soloud _soloud;
        private static Sound _overworldBGM;
        private static Song _newOverworldBGM;
        private static Sound _battleBGM;

        static SoundUtils()
        {
            _soloud = new Soloud();
        }
        public static void Init()
        {
            _soloud.init(Soloud.CLIP_ROUNDOFF | Soloud.SDL2);
        }
        public static void DeInit()
        {
            _soloud.deinit();
        }
        // Ideally we would want to be using wav.loadFile(), but I'm not sure if it's possible from C#
        private static unsafe Sound SongToSound(Song song)
        {
            byte[] bytes;
            (string resource, double loopPoint) = _songResources[song];
            using (Stream stream = Utils.GetResourceStream(resource))
            {
                bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
            }
            var wav = new WavStream();
            fixed (byte* b = bytes)
            {
                wav.loadMem(new IntPtr(b), (uint)bytes.Length, aCopy: 1);
            }
            if (loopPoint != 0)
            {
                wav.setLoopPoint(loopPoint);
            }
            wav.setLooping(1);
            return new Sound(song, wav);
        }

        public static void SetOverworldBGM_NoFade(Song song)
        {
            if (_overworldBGM != null)
            {
                _soloud.stop(_overworldBGM.Handle);
            }
            _overworldBGM = SongToSound(song);
            _overworldBGM.Handle = _soloud.playBackground(_overworldBGM.Wav);
        }
        public static void SetOverworldBGM(Song song)
        {
            if (_overworldBGM is null)
            {
                _overworldBGM = SongToSound(song);
                _overworldBGM.Handle = _soloud.playBackground(_overworldBGM.Wav);
                return;
            }
            // No need to do anything if it's the same song
            if (_overworldBGM.Song == song)
            {
                return;
            }
            // Fade to nothing
            if (song == Song.None)
            {
                _soloud.fadeVolume(_overworldBGM.Handle, 0, 1);
                _soloud.scheduleStop(_overworldBGM.Handle, 1);
                Game.Instance.SetSCallback(SCB_FadingOutOverworldToNothing);
                return;
            }
            // Fade to something
            _newOverworldBGM = song;
            _soloud.fadeVolume(_overworldBGM.Handle, 0, 1);
            _soloud.scheduleStop(_overworldBGM.Handle, 1);
            Game.Instance.SetSCallback(SCB_FadingOutOverworldToOverworld);
        }

        public static void FadeOutBattleBGMToOverworldBGM()
        {
            _soloud.fadeVolume(_battleBGM.Handle, 0, 1);
            _soloud.scheduleStop(_battleBGM.Handle, 1);
            Game.Instance.SetSCallback(SCB_FadingOutBattleToOverworld);
        }
        public static void SetBattleBGM(Song song)
        {
            // Assuming you're not setting battle bgm twice in a row
            if (_overworldBGM != null)
            {
                _soloud.setPause(_overworldBGM.Handle, 1);
                _soloud.setVolume(_overworldBGM.Handle, 0);
            }
            _battleBGM = SongToSound(song);
            _battleBGM.Handle = _soloud.playBackground(_battleBGM.Wav);
        }

        private static void SCB_FadingOutBattleToOverworld()
        {
            if (_soloud.getVolume(_battleBGM.Handle) <= 0)
            {
                _soloud.stop(_battleBGM.Handle);
                _battleBGM = null;
                if (_overworldBGM != null)
                {
                    _soloud.setPause(_overworldBGM.Handle, 0);
                    _soloud.fadeVolume(_overworldBGM.Handle, 1, 1);
                }
                Game.Instance.SetSCallback(null);
            }
        }
        private static void SCB_FadingOutOverworldToOverworld()
        {
            if (_soloud.getVolume(_overworldBGM.Handle) <= 0)
            {
                _soloud.stop(_overworldBGM.Handle);
                _overworldBGM = SongToSound(_newOverworldBGM);
                _newOverworldBGM = Song.None;
                _overworldBGM.Handle = _soloud.playBackground(_overworldBGM.Wav);
                Game.Instance.SetSCallback(null);
            }
        }
        private static void SCB_FadingOutOverworldToNothing()
        {
            if (_soloud.getVolume(_overworldBGM.Handle) <= 0)
            {
                _soloud.stop(_overworldBGM.Handle);
                _overworldBGM = null;
                Game.Instance.SetSCallback(null);
            }
        }
    }
}
