// CMR (phase B): Archipelago connection (Archipelago.MultiClient.Net, MIT).
// Network work happens on background tasks; every effect on the game is queued and applied on the main thread
// (ApClient.Update). Nothing received from the server is ever executed or downloaded: we only read item names,
// player names and the slot_data JSON (parsed with a bounded parser).
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Packets;
using Monocle;
using Newtonsoft.Json;

namespace Celeste.Mod.UltraRando.AP {
    public static class ApClient {
        static ArchipelagoSession session;
        static DeathLinkService deathLink;
        static volatile bool connecting;
        static readonly ConcurrentQueue<Action> mainThread = new ConcurrentQueue<Action>();
        static int generation; // incremented on each connect/disconnect, stale callbacks are dropped

        public static bool Connected => session != null && ApState.Mode == ApMode.Online;
        public static bool Connecting => connecting;

        static void Post(int gen, Action a) {
            mainThread.Enqueue(() => { if (gen == generation) a(); });
        }

        /// <summary>Main thread, every frame.</summary>
        public static void Update() {
            int n = 0;
            while (n++ < 256 && mainThread.TryDequeue(out var a)) {
                try { a(); } catch (Exception e) { ApState.Log("main-thread action failed: " + e); }
            }
            ApState.PollSave();
            DeathLinkBridge.Update();
            AP.SelfTest.Update();
        }

        public static bool ParseServer(string server, out Uri uri, out string host, out int port, out string error) {
            uri = null; host = null; port = 38281; error = null;
            server = (server ?? "").Trim();
            if (server.Length == 0 || server.Length > 200) { error = "adresse vide ou trop longue"; return false; }
            foreach (char c in server) {
                if (char.IsWhiteSpace(c) || char.IsControl(c)) { error = "caractère invalide dans l'adresse"; return false; }
            }
            if (server.Contains("://")) {
                if (!Uri.TryCreate(server, UriKind.Absolute, out uri) || (uri.Scheme != "ws" && uri.Scheme != "wss")) {
                    error = "adresse invalide (ws:// ou wss:// attendu)"; return false;
                }
                return true;
            }
            int colon = server.LastIndexOf(':');
            if (colon > 0) {
                host = server.Substring(0, colon);
                if (!int.TryParse(server.Substring(colon + 1), out port) || port <= 0 || port > 65535) { error = "port invalide"; return false; }
            } else {
                host = server;
            }
            return true;
        }

        public static void Connect(string server, string slot, string password) {
            if (connecting) return;
            slot = (slot ?? "").Trim();
            if (slot.Length == 0 || slot.Length > 16 * 4) { ApState.Status = "Nom de slot vide ou trop long"; return; }
            if (!ParseServer(server, out var uri, out var host, out var port, out var err)) { ApState.Status = "Erreur : " + err; return; }
            Disconnect(false);
            int gen = ++generation;
            connecting = true;
            ApState.Status = "Connexion à " + Text.Sanitize(server, 64) + "...";
            ApState.Log("connecting to " + Text.Sanitize(server, 64) + " as " + Text.Sanitize(slot, 32));
            Task.Run(() => {
                ArchipelagoSession s = null;
                try {
                    s = uri != null ? ArchipelagoSessionFactory.CreateSession(uri) : ArchipelagoSessionFactory.CreateSession(host, port);
                    var result = s.TryConnectAndLogin(ApState.GameName, slot, ItemsHandlingFlags.AllItems,
                        tags: new string[0], password: string.IsNullOrEmpty(password) ? null : password, requestSlotData: true);
                    if (result is LoginSuccessful ok) {
                        string json = JsonConvert.SerializeObject(ok.SlotData ?? new Dictionary<string, object>());
                        var slotData = SlotData.Parse(json);
                        string seed = s.RoomState?.Seed ?? "";
                        Post(gen, () => OnLoggedIn(s, slotData, json, seed, slot));
                    } else {
                        var fail = result as LoginFailure;
                        string why = fail == null ? "échec" : string.Join(", ", (fail.Errors ?? new string[0]).Take(4));
                        Post(gen, () => { connecting = false; ApState.Status = "Échec : " + Text.Sanitize(why, 120); });
                        TryClose(s);
                    }
                } catch (Exception e) {
                    ApState.Log("connection failed: " + e);
                    Post(gen, () => { connecting = false; ApState.Status = "Échec : " + Text.Sanitize(e.Message, 120); });
                    TryClose(s);
                }
            });
        }

        static void OnLoggedIn(ArchipelagoSession s, SlotData slotData, string json, string seed, string slot) {
            connecting = false;
            session = s;
            int gen = generation;
            ApState.PendingCacheJson = json;
            ApState.StartOnline(slotData, seed, slot);

            s.Socket.SocketClosed += reason => Post(gen, () => {
                session = null;
                deathLink = null;
                ApState.ConnectionLost(reason ?? "fermé");
            });
            s.Items.ItemReceived += helper => {
                // socket thread: dequeue everything with its index, apply on the main thread
                var batch = new List<(int, string, string)>();
                while (helper.Any()) {
                    int index = helper.Index;
                    var item = helper.DequeueItem();
                    if (item == null) break;
                    string name = item.ItemName ?? helper.GetItemName(item.ItemId) ?? ("#" + item.ItemId);
                    string from = item.Player?.Name ?? "";
                    batch.Add((index, name, from));
                }
                Post(gen, () => { foreach (var (i, n, f) in batch) ApState.ItemReceived(i, n, f); });
            };
            // big notifications: every item that I receive, find, or send (the server's ItemSend messages; hints excluded)
            s.MessageLog.OnMessageReceived += msg => {
                if (msg is HintItemSendLogMessage || !(msg is ItemSendLogMessage m)) return;
                int me = s.ConnectionInfo.Slot;
                bool toMe = m.Receiver?.Slot == me, fromMe = m.Sender?.Slot == me;
                if (!toMe && !fromMe) return;
                var it = m.Item;
                string item = it?.ItemName, game = it?.ItemGame, loc = it?.LocationName;
                string other = toMe ? (m.Sender?.Alias ?? m.Sender?.Name) : (m.Receiver?.Alias ?? m.Receiver?.Name);
                var flags = it?.Flags ?? ItemFlags.None;
                Post(gen, () => ApHud.ItemEvent(toMe, fromMe, item, game, loc, other, flags));
            };
            // contents of every check of the seed (no hint is created): the strawberry badge shows whether a berry
            // holds something important or only a filler item
            try {
                var ids = ApState.Slot.LocationIds.Values.ToArray();
                s.Locations.ScoutLocationsAsync(false, ids).ContinueWith(t => {
                    if (t.Status != TaskStatus.RanToCompletion || t.Result == null) { ApState.Log("scout failed: " + t.Exception?.GetBaseException().Message); return; }
                    var res = t.Result.ToDictionary(kv => kv.Key, kv => kv.Value.Flags);
                    Post(gen, () => ApState.SetScouts(res));
                });
            } catch (Exception e) { ApState.Log("scout failed: " + e.Message); }
            // items that arrived before the handler was attached
            {
                var batch = new List<(int, string, string)>();
                var helper = s.Items;
                while (helper.Any()) {
                    int index = helper.Index;
                    var item = helper.DequeueItem();
                    if (item == null) break;
                    batch.Add((index, item.ItemName ?? helper.GetItemName(item.ItemId) ?? ("#" + item.ItemId), item.Player?.Name ?? ""));
                }
                foreach (var (i, n, f) in batch) ApState.ItemReceived(i, n, f);
            }

            if (slotData.Option("death_link", 0) != 0) {
                try {
                    deathLink = s.CreateDeathLinkService();
                    deathLink.OnDeathLinkReceived += dl => Post(gen, () => DeathLinkBridge.Received(Text.Sanitize(dl?.Source, 32), Text.Sanitize(dl?.Cause, 80)));
                    deathLink.EnableDeathLink();
                    ApState.Log("DeathLink enabled");
                } catch (Exception e) {
                    ApState.Log("DeathLink setup failed: " + e.Message);
                }
            }
            ApState.FlushChecks();
        }

        public static void SendChecks(long[] ids) {
            var s = session;
            if (s == null || ids == null || ids.Length == 0) return;
            Task.Run(() => {
                try { s.Locations.CompleteLocationChecks(ids); } catch (Exception e) { ApState.Log("check send failed: " + e.Message); }
            });
        }

        public static void SendGoal() {
            var s = session;
            if (s == null) return;
            Task.Run(() => {
                try { s.SetGoalAchieved(); } catch (Exception e) { ApState.Log("goal send failed: " + e.Message); }
            });
        }

        public static void SendDeath(string cause) {
            var dl = deathLink;
            var s = session;
            if (dl == null || s == null) return;
            string me = ApState.SlotName;
            Task.Run(() => {
                try { dl.SendDeathLink(new DeathLink(me, cause)); } catch (Exception e) { ApState.Log("DeathLink send failed: " + e.Message); }
            });
        }

        public static void Disconnect(bool stopSession = true) {
            generation++;
            connecting = false;
            var s = session;
            session = null;
            deathLink = null;
            TryClose(s);
            if (stopSession && (ApState.Mode == ApMode.Online || ApState.Mode == ApMode.Cached)) ApState.Stop();
        }

        static void TryClose(ArchipelagoSession s) {
            if (s == null) return;
            Task.Run(async () => {
                try { await s.Socket.DisconnectAsync(); } catch { }
            });
        }

        /// <summary>Startup self-test: proves the Archipelago and Newtonsoft assemblies resolve inside Everest.</summary>
        public static string AssemblyCheck() {
            var s = ArchipelagoSessionFactory.CreateSession("localhost", 38281);
            string json = JsonConvert.SerializeObject(new Dictionary<string, object> { { "a", 1L } });
            return $"MultiClient {typeof(ArchipelagoSession).Assembly.GetName().Version}, Newtonsoft {typeof(JsonConvert).Assembly.GetName().Version} ({json}), session object {(s != null ? "ok" : "null")}";
        }
    }

    /// <summary>DeathLink glue: death sending (Player.Die hook) and received deaths (applied in a level).</summary>
    public static class DeathLinkBridge {
        static bool pendingKill;
        static string pendingText;
        public static bool Killing;
        public static int KillsForTest;

        public static void Received(string source, string cause) {
            pendingKill = true;
            pendingText = string.IsNullOrEmpty(cause) ? source + " est mort(e)" : cause;
        }

        public static void Update() {
            if (!pendingKill) return;
            if (!(Engine.Scene is Level level) || level.Paused || level.Transitioning) return;
            if (!ApState.SaveMatches(false)) { pendingKill = false; return; } // vanilla save: never killed
            var player = level.Tracker.GetEntity<Player>();
            if (player == null || player.Dead) return;
            pendingKill = false;
            ApHud.Message("DeathLink : " + pendingText);
            KillsForTest++;
            Killing = true;
            try { player.Die(Microsoft.Xna.Framework.Vector2.Zero, true, true); } finally { Killing = false; }
        }
    }
}
