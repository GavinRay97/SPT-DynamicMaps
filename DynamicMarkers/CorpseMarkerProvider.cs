using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using DynamicMaps.Data;
using DynamicMaps.Patches;
using DynamicMaps.UI.Components;
using DynamicMaps.Utils;
using EFT;
using EFT.Interactive;
using HarmonyLib;
using UnityEngine;

namespace DynamicMaps.DynamicMarkers
{
    public class CorpseMarkerProvider : IDynamicMarkerProvider
    {
        private static FieldInfo _playerCorpseField = AccessTools.Field(typeof(Player), "Corpse");

        private const string _skullImagePath = "Markers/skull.png";

        // TODO: move to config
        private const string _friendlyCorpseCategory = "Friendly Corpse";
        private const string _friendlyCorpseImagePath = _skullImagePath;
        private static Color _friendlyCorpseColor = Color.blue;

        private const string _killedCorpseCategory = "Killed Corpse";
        private const string _killedCorpseImagePath = _skullImagePath;
        private static Color _killedCorpseColor = Color.red;

        private const string _killedBossCorpseCategory = "Killed Boss Corpse";
        private const string _killedBossCorpseImagePath = _skullImagePath;
        private static Color _killedBossCorpseColor = Color.magenta;

        private const string _bossCorpseCategory = "Boss Corpse";
        private const string _bossCorpseImagePath = _skullImagePath;
        private static Color _bossCorpseColor = Color.magenta;

        private const string _otherCorpseCategory = "Other Corpse";
        private const string _otherCorpseImagePath = _skullImagePath;
        private static Color _otherCorpseColor = Color.white;
        //

        private bool _showFriendlyCorpses = true;
        public bool ShowFriendlyCorpses
        {
            get
            {
                return _showFriendlyCorpses;
            }

            set
            {
                HandleSetBoolOption(ref _showFriendlyCorpses, value);
            }
        }

        private bool _showKilledCorpses = true;
        public bool ShowKilledCorpses
        {
            get
            {
                return _showKilledCorpses;
            }

            set
            {
                HandleSetBoolOption(ref _showKilledCorpses, value);
            }
        }

        private bool _showBossCorpses = false;
        public bool ShowBossCorpses
        {
            get
            {
                return _showBossCorpses;
            }

            set
            {
                HandleSetBoolOption(ref _showBossCorpses, value);
            }
        }

        private bool _showOtherCorpses = false;
        public bool ShowOtherCorpses
        {
            get
            {
                return _showOtherCorpses;
            }

            set
            {
                HandleSetBoolOption(ref _showOtherCorpses, value);
            }
        }

        private MapView _lastMapView;
        private Dictionary<Player, MapMarker> _corpseMarkers = new Dictionary<Player, MapMarker>();

        public void OnShowInRaid(MapView map)
        {
            _lastMapView = map;

            TryAddMarkers();

            GameWorldUnregisterPlayerPatch.OnUnregisterPlayer += OnUnregisterPlayer;
        }

        public void OnHideInRaid(MapView map)
        {
            GameWorldUnregisterPlayerPatch.OnUnregisterPlayer -= OnUnregisterPlayer;
        }

        public void OnRaidEnd(MapView map)
        {
            GameWorldUnregisterPlayerPatch.OnUnregisterPlayer -= OnUnregisterPlayer;

            _lastMapView = map;
            TryRemoveMarkers();
        }

        public void OnMapChanged(MapView map, MapDef mapDef)
        {
            _lastMapView = map;

            foreach (var corpse in _corpseMarkers.Keys.ToList())
            {
                TryRemoveMarker(corpse);
                TryAddMarker(corpse);
            }
        }

        public void OnDisable(MapView map)
        {
            GameWorldUnregisterPlayerPatch.OnUnregisterPlayer -= OnUnregisterPlayer;
            TryRemoveMarkers();
        }

        private void OnUnregisterPlayer(IPlayer iPlayer)
        {
            if (!(iPlayer is Player))
            {
                return;
            }

            var player = iPlayer as Player;
            var corpse = _playerCorpseField.GetValue(player) as Corpse;
            if (corpse != null)
            {
                TryAddMarker(player);
            }
        }

        private void TryRemoveMarkers()
        {
            foreach (var corpse in _corpseMarkers.Keys.ToList())
            {
                TryRemoveMarker(corpse);
            }

            _corpseMarkers.Clear();
        }

        private void TryAddMarkers()
        {
            if (!GameUtils.IsInRaid())
            {
                return;
            }

            // add all players that have spawned already in raid
            var gameWorld = Singleton<GameWorld>.Instance;
            foreach (var player in gameWorld.AllPlayersEverExisted)
            {
                var corpse = _playerCorpseField.GetValue(player) as Corpse;
                if (corpse == null || _corpseMarkers.ContainsKey(player))
                {
                    continue;
                }

                TryAddMarker(player);
            }
        }

        private void TryAddMarker(Player player)
        {
            if (_lastMapView == null || _corpseMarkers.ContainsKey(player))
            {
                return;
            }

            // set category and color
            var category = _otherCorpseCategory;
            var imagePath = _otherCorpseImagePath;
            var color = _otherCorpseColor;

            if (player.IsGroupedWithMainPlayer())
            {
                category = _friendlyCorpseCategory;
                imagePath = _friendlyCorpseImagePath;
                color = _friendlyCorpseColor;
            }
            else if (player.IsTrackedBoss() && player.DidMainPlayerKill())
            {
                category = _killedBossCorpseCategory;
                imagePath = _killedBossCorpseImagePath;
                color = _killedBossCorpseColor;
            }
            else if (player.DidMainPlayerKill())
            {
                category = _killedCorpseCategory;
                imagePath = _killedCorpseImagePath;
                color = _killedCorpseColor;
            }
            else if (player.IsTrackedBoss())
            {
                category = _bossCorpseCategory;
                imagePath = _bossCorpseImagePath;
                color = _bossCorpseColor;
            }

            if (!ShouldShowCategory(category))
            {
                return;
            }

            var markerDef = new MapMarkerDef
            {
                Category = category,
                ImagePath = imagePath,
                Text = player.Profile.GetCorrectedNickname(),
                Color = color,
                Position = MathUtils.ConvertToMapPosition(player.Position)
            };

            // try adding marker
            var marker = _lastMapView.AddMapMarker(markerDef);
            _corpseMarkers[player] = marker;
        }

        private void RemoveDisabledMarkers()
        {
            foreach (var corpse in _corpseMarkers.Keys.ToList())
            {
                var marker = _corpseMarkers[corpse];
                if (!ShouldShowCategory(marker.Category))
                {
                    TryRemoveMarker(corpse);
                }
            }
        }

        private void TryRemoveMarker(Player player)
        {
            if (!_corpseMarkers.ContainsKey(player))
            {
                return;
            }

            _corpseMarkers[player].ContainingMapView.RemoveMapMarker(_corpseMarkers[player]);
            _corpseMarkers.Remove(player);
        }

        private bool ShouldShowCategory(string category)
        {
            switch (category)
            {
                case _friendlyCorpseCategory:
                    return _showFriendlyCorpses;
                case _killedCorpseCategory:
                case _killedBossCorpseCategory:
                    return _showKilledCorpses;
                case _bossCorpseCategory:
                    return _showOtherCorpses;
                case _otherCorpseCategory:
                    return _showOtherCorpses;
                default:
                    return false;
            }
        }

        private void HandleSetBoolOption(ref bool boolOption, bool value)
        {
            if (value == boolOption)
            {
                return;
            }

            boolOption = value;

            if (boolOption)
            {
                TryAddMarkers();
            }
            else
            {
                RemoveDisabledMarkers();
            }
        }

        public void OnShowOutOfRaid(MapView map)
        {
            // do nothing
        }

        public void OnHideOutOfRaid(MapView map)
        {
            // do nothing
        }
    }
}
