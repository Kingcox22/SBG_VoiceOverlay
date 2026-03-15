using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ProximityChat;
using UnityEngine;

namespace VoiceActivityOverlay
{
    [BepInPlugin("com.kingcox22.sbg.voiceoverlay", "SBG-Voice Overlay", "1.0.0")]
    public class VoiceOverlayPlugin : BaseUnityPlugin
    {
        private static List<VoiceNetworker> _allNetworkers = new List<VoiceNetworker>();
        private static GUIStyle _labelStyle;
        
        // Reflection Cache
        private static MemberInfo _volumeMember;
        private static bool _searched = false;

        private ConfigEntry<float> _offsetX;
        private ConfigEntry<float> _offsetY;
        private ConfigEntry<int> _fontSize;

        void Awake()
        {
            var sizeRange = new AcceptableValueRange<int>(1, 32);
            var posRange = new AcceptableValueRange<float>(0f, 3000f);

            _offsetX = Config.Bind("General", "X Offset", 20f, new ConfigDescription("Horizontal position.", posRange));
            _offsetY = Config.Bind("General", "Y Offset", 20f, new ConfigDescription("Vertical starting position.", posRange));
            _fontSize = Config.Bind("General", "Font Size", 18, new ConfigDescription("Size of the text.", sizeRange));

            new Harmony("com.kingcox22.sbg.voiceoverlay").PatchAll();
            Logger.LogInfo("Voice Activity Overlay Loaded!");
        }

        [HarmonyPatch(typeof(VoiceNetworker), "OnStartClient")]
        public static class VoiceNetworker_Patch
        {
            static void Postfix(VoiceNetworker __instance)
            {
                if (!_allNetworkers.Contains(__instance))
                    _allNetworkers.Add(__instance);
            }
        }

        void OnGUI()
        {
            if (_allNetworkers.Count == 0) return;

            if (!_searched)
            {
                _searched = true;
                Type t = typeof(VoiceNetworker);
                string[] possibleNames = { "SlowSmoothedNormalizedVolume", "SmoothedNormalizedVolume", "normalizedVolume" };
                
                foreach (var name in possibleNames)
                {
                    // Check Properties
                    _volumeMember = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (_volumeMember != null) break;

                    // Check Fields (just in case it's a raw float)
                    _volumeMember = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (_volumeMember != null) break;
                }

                if (_volumeMember != null)
                    Logger.LogInfo($"Found volume member: {_volumeMember.Name} via reflection.");
                else
                    Logger.LogError("Could not find any volume property/field on VoiceNetworker.");
            }

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, richText = true };
            }

            _labelStyle.fontSize = _fontSize.Value;
            float xPos = _offsetX.Value;
            float yOffset = _offsetY.Value;
            
            _allNetworkers.RemoveAll(v => v == null);

            foreach (var networker in _allNetworkers)
            {
                if (networker.IsTalking)
                {
                    var playerGolfer = networker.GetComponentInParent<PlayerGolfer>();
                    string name = playerGolfer != null ? GetPlayerName(playerGolfer.PlayerInfo) : "Unknown";
                    
                    float volume = 0f;
                    if (_volumeMember is PropertyInfo prop)
                        volume = (float)prop.GetValue(networker);
                    else if (_volumeMember is FieldInfo field)
                        volume = (float)field.GetValue(networker);

                    // If volume is very low, give it at least 1 bar if they are "talking" 
                    // to confirm the bar is actually rendering.
                    int barCount = Mathf.Max(1, (int)(volume * 20));
                    string volumeBar = new string('|', barCount);

                    string shadowText = $"[VOICE] {name} {volumeBar}";
                    string frontText = $"<color=#00FF00>[VOICE]</color> {name} {volumeBar}";

                    GUI.color = Color.black;
                    GUI.Label(new Rect(xPos + 2, yOffset + 2, 600, 35), shadowText, _labelStyle);
                    
                    GUI.color = Color.white;
                    GUI.Label(new Rect(xPos, yOffset, 600, 35), frontText, _labelStyle);
                    
                    yOffset += (_fontSize.Value + 7);
                }
            }
        }

        private string GetPlayerName(PlayerInfo info)
        {
            if (info == null) return "Unknown";
            var type = info.GetType();
            var field = type.GetField("playerName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) return field.GetValue(info)?.ToString() ?? "Unknown";

            Component playerId = info.GetComponent("PlayerId");
            if (playerId != null)
            {
                var f = playerId.GetType().GetField("playerName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null) return f.GetValue(playerId)?.ToString() ?? "Unknown";
            }
            return "Golfer"; 
        }
    }
}