using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using EntityComponent;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Mods;
using JumpKing.Player;
using JumpKing.Util;
using JumpKing.Util.Tags;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KeyedSaveStates
{
    [JumpKingMod("eski4869.KeyedSaveStates")]
    public static class ModEntry
    {
        public const string CommandTarget = "keyed_save_states";

        private static KeyedSaveStatesBehaviour _registeredBehaviour;

        [BeforeLevelLoad]
        public static void BeforeLevelLoad()
        {
            KeyedSaveStateStore.EnsureLoaded();
            BrokerCommandClient.Register(CommandTarget);
        }

        [OnLevelStart]
        public static void OnLevelStart()
        {
            KeyedSaveStateStore.EnsureLoaded();
            BrokerCommandClient.Register(CommandTarget);
            KeyedSaveStatesOverlay.EnsureAdded();

            PlayerEntity player = EntityManager.instance.Find<PlayerEntity>();
            if (player == null)
            {
                return;
            }

            if (_registeredBehaviour != null)
            {
                try
                {
                    player.m_body.RemoveBehaviour(_registeredBehaviour);
                }
                catch
                {
                }
            }

            _registeredBehaviour = new KeyedSaveStatesBehaviour();
            player.m_body.RegisterBehaviour(_registeredBehaviour);
        }

        [OnLevelUnload]
        public static void OnLevelUnload()
        {
            _registeredBehaviour = null;
        }

        [OnLevelEnd]
        public static void OnLevelEnd()
        {
            _registeredBehaviour = null;
        }
    }

    public sealed class KeyedSaveStatesBehaviour : IBodyCompBehaviour
    {
        public bool ExecuteBehaviour(BehaviourContext behaviourContext)
        {
            string command;
            if (!BrokerCommandClient.TryDequeue(ModEntry.CommandTarget, out command))
            {
                return true;
            }

            KeyedSaveStatesRuntime.ExecuteCommand(command);
            return true;
        }
    }

    internal static class KeyedSaveStatesRuntime
    {
        public static string DisplayText { get; private set; }
        public static float MessageSeconds { get; private set; }

        public static bool HasDisplay
        {
            get { return MessageSeconds > 0f && !string.IsNullOrEmpty(DisplayText); }
        }

        public static void ExecuteCommand(string command)
        {
            string action;
            string key;
            if (!TryParseCommand(command, out action, out key))
            {
                return;
            }

            if (action == "save")
            {
                Save(key);
            }
            else if (action == "load")
            {
                Load(key);
            }
        }

        public static void TickMessage(float delta)
        {
            if (MessageSeconds <= 0f)
            {
                return;
            }

            MessageSeconds = Math.Max(0f, MessageSeconds - delta);
        }

        private static bool TryParseCommand(string command, out string action, out string key)
        {
            action = null;
            key = null;

            if (string.IsNullOrWhiteSpace(command))
            {
                return false;
            }

            string[] parts = command.Split(',');
            if (parts.Length != 2)
            {
                return false;
            }

            action = parts[0].Trim().ToLowerInvariant();
            key = parts[1].Trim();

            if ((action != "save" && action != "load") || key.Length == 0)
            {
                return false;
            }

            return true;
        }

        private static void Save(string key)
        {
            PlayerEntity player = EntityManager.instance.Find<PlayerEntity>();
            if (player == null)
            {
                Show("Save failed: no player");
                return;
            }

            KeyedSaveState state = KeyedSaveState.FromPlayer(key, player);
            KeyedSaveStateStore.Set(state);
            KeyedSaveStateStore.Save();
            PlaySaveSound();
            Show("Saved: " + key);
        }

        private static void Load(string key)
        {
            PlayerEntity player = EntityManager.instance.Find<PlayerEntity>();
            if (player == null)
            {
                Show("Load failed: no player");
                return;
            }

            KeyedSaveState state;
            if (!KeyedSaveStateStore.TryGet(key, out state))
            {
                Show("No save: " + key);
                return;
            }

            player.m_body.Position = state.Position;
            player.m_body.Velocity = Vector2.Zero;
            JumpKing.Camera.UpdateCamera(player.m_body.GetHitbox().Center);
            PlayLoadSound();
            Show("Loaded: " + key);
        }

        private static void Show(string text)
        {
            DisplayText = text;
            MessageSeconds = 2f;
        }

        private static void PlaySaveSound()
        {
            try
            {
                if (Game1.instance != null &&
                    Game1.instance.contentManager != null &&
                    Game1.instance.contentManager.audio != null &&
                    Game1.instance.contentManager.audio.Plink != null)
                {
                    Game1.instance.contentManager.audio.Plink.PlayOneShot();
                }
            }
            catch
            {
            }
        }

        private static void PlayLoadSound()
        {
            try
            {
                if (Game1.instance != null &&
                    Game1.instance.contentManager != null &&
                    Game1.instance.contentManager.audio != null &&
                    Game1.instance.contentManager.audio.menu != null &&
                    Game1.instance.contentManager.audio.menu.Select != null)
                {
                    Game1.instance.contentManager.audio.menu.Select.PlayOneShot();
                }
            }
            catch
            {
            }
        }
    }

    internal static class KeyedSaveStateStore
    {
        private const string StateFileName = "keyed_save_states.tsv";
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, KeyedSaveState> States = new Dictionary<string, KeyedSaveState>(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded;
        private static string _statePath;

        public static void EnsureLoaded()
        {
            lock (Sync)
            {
                EnsureStatePath();

                if (_loaded)
                {
                    return;
                }

                Load();
                _loaded = true;
            }
        }

        public static void Set(KeyedSaveState state)
        {
            EnsureLoaded();

            lock (Sync)
            {
                States[state.Key] = state;
            }
        }

        public static bool TryGet(string key, out KeyedSaveState state)
        {
            EnsureLoaded();

            lock (Sync)
            {
                return States.TryGetValue(key, out state);
            }
        }

        public static void Save()
        {
            lock (Sync)
            {
                EnsureStatePath();

                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("key\tscreen\tx\ty");

                    foreach (KeyedSaveState state in States.Values)
                    {
                        sb.AppendLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "{0}\t{1}\t{2}\t{3}",
                            EscapeKey(state.Key),
                            state.Screen,
                            state.Position.X,
                            state.Position.Y
                        ));
                    }

                    File.WriteAllText(_statePath, sb.ToString(), new UTF8Encoding(false));
                }
                catch (Exception ex)
                {
                    JumpKing.Program.crashLog.AddErrorMessage("KeyedSaveStates save failed: " + ex.Message);
                }
            }
        }

        private static void Load()
        {
            States.Clear();

            try
            {
                if (!File.Exists(_statePath))
                {
                    return;
                }

                string[] lines = File.ReadAllLines(_statePath);
                for (int i = 1; i < lines.Length; i++)
                {
                    KeyedSaveState state;
                    if (KeyedSaveState.TryParse(lines[i], out state))
                    {
                        States[state.Key] = state;
                    }
                }
            }
            catch (Exception ex)
            {
                JumpKing.Program.crashLog.AddErrorMessage("KeyedSaveStates load failed: " + ex.Message);
            }
        }

        private static void EnsureStatePath()
        {
            if (!string.IsNullOrEmpty(_statePath))
            {
                return;
            }

            string assemblyPath = typeof(ModEntry).Assembly.Location;
            string directory = Path.GetDirectoryName(assemblyPath);
            _statePath = Path.Combine(directory, StateFileName);
        }

        private static string EscapeKey(string key)
        {
            return key.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
        }
    }

    internal struct KeyedSaveState
    {
        public string Key;
        public int Screen;
        public Vector2 Position;

        public static KeyedSaveState FromPlayer(string key, PlayerEntity player)
        {
            return new KeyedSaveState
            {
                Key = key,
                Screen = JumpKing.Camera.CurrentScreen + 1,
                Position = player.m_body.Position
            };
        }

        public static bool TryParse(string line, out KeyedSaveState state)
        {
            state = new KeyedSaveState();

            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            string[] parts = line.Split('\t');
            if (parts.Length != 4)
            {
                return false;
            }

            int screen;
            float x;
            float y;

            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out screen) ||
                !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
                !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out y))
            {
                return false;
            }

            state = new KeyedSaveState
            {
                Key = parts[0],
                Screen = screen,
                Position = new Vector2(x, y)
            };

            return state.Key.Length > 0;
        }
    }

    public sealed class KeyedSaveStatesOverlay : Entity, IForeground
    {
        private static KeyedSaveStatesOverlay _instance;
        private Texture2D _pixel;

        public static void EnsureAdded()
        {
            if (EntityManager.instance == null)
            {
                return;
            }

            if (_instance != null && _instance.IsAlive)
            {
                return;
            }

            _instance = new KeyedSaveStatesOverlay();
            EntityManager.instance.AddObject(_instance);
        }

        protected override void Update(float delta)
        {
            KeyedSaveStatesRuntime.TickMessage(delta);
        }

        public void ForegroundDraw()
        {
            if (!KeyedSaveStatesRuntime.HasDisplay)
            {
                return;
            }

            EnsurePixel();

            SpriteFont font = GetFont();
            if (font == null)
            {
                return;
            }

            string text = KeyedSaveStatesRuntime.DisplayText;
            Vector2 size = font.MeasureString(text);
            int paddingX = 8;
            int paddingY = 5;
            int width = (int)Math.Ceiling(size.X) + paddingX * 2;
            int height = (int)Math.Ceiling(size.Y) + paddingY * 2;
            int x = 480 - width - 10;
            int y = 10;

            Game1.spriteBatch.Draw(_pixel, new Rectangle(x, y, width, height), new Color((byte)0, (byte)0, (byte)0, (byte)185));
            Game1.spriteBatch.Draw(_pixel, new Rectangle(x, y, width, 1), Color.Gray);
            Game1.spriteBatch.Draw(_pixel, new Rectangle(x, y + height - 1, width, 1), Color.Gray);
            Game1.spriteBatch.Draw(_pixel, new Rectangle(x, y, 1, height), Color.Gray);
            Game1.spriteBatch.Draw(_pixel, new Rectangle(x + width - 1, y, 1, height), Color.Gray);

            TextHelper.DrawString(font, text, new Vector2(x + paddingX, y + paddingY), Color.White, Vector2.Zero, true);
        }

        protected override void OnDestroy()
        {
            if (_pixel != null)
            {
                _pixel.Dispose();
                _pixel = null;
            }

            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
        }

        private void EnsurePixel()
        {
            if (_pixel != null || Game1.instance == null)
            {
                return;
            }

            _pixel = new Texture2D(Game1.instance.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        private static SpriteFont GetFont()
        {
            if (Game1.instance == null || Game1.instance.contentManager == null)
            {
                return null;
            }

            if (Game1.instance.contentManager.font.MenuFontSmall != null)
            {
                return Game1.instance.contentManager.font.MenuFontSmall;
            }

            return Game1.instance.contentManager.font.MenuFont;
        }
    }

    internal static class BrokerCommandClient
    {
        private const string RegistryTypeName = "JumpKingHttpCommandBroker.CommandQueueRegistry";

        private static object _registry;
        private static MethodInfo _registerMethod;
        private static MethodInfo _tryDequeueMethod;
        private static DateTime _nextResolveUtc = DateTime.MinValue;
        private static bool _loggedMissingBroker;
        private static bool _registered;

        public static void Register(string target)
        {
            if (_registered)
            {
                return;
            }

            if (!Resolve())
            {
                return;
            }

            try
            {
                _registerMethod.Invoke(_registry, new object[] { target });
                _registered = true;
            }
            catch (Exception ex)
            {
                JumpKing.Program.crashLog.AddErrorMessage("KeyedSaveStates broker register failed: " + ex.Message);
            }
        }

        public static bool TryDequeue(string target, out string command)
        {
            command = null;

            if (!_registered)
            {
                Register(target);
            }

            if (!_registered || !Resolve())
            {
                return false;
            }

            try
            {
                object[] args = new object[] { target, null };
                bool dequeued = (bool)_tryDequeueMethod.Invoke(_registry, args);
                command = args[1] as string;
                return dequeued;
            }
            catch (Exception ex)
            {
                JumpKing.Program.crashLog.AddErrorMessage("KeyedSaveStates broker dequeue failed: " + ex.Message);
                return false;
            }
        }

        private static bool Resolve()
        {
            if (_registry != null)
            {
                return true;
            }

            DateTime nowUtc = DateTime.UtcNow;
            if (nowUtc < _nextResolveUtc)
            {
                return false;
            }

            _nextResolveUtc = nowUtc.AddSeconds(1);

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type registryType = assemblies[i].GetType(RegistryTypeName, false);
                if (registryType == null)
                {
                    continue;
                }

                FieldInfo instanceField = registryType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                MethodInfo registerMethod = registryType.GetMethod("Register", new Type[] { typeof(string) });
                MethodInfo tryDequeueMethod = registryType.GetMethod("TryDequeue", new Type[] { typeof(string), typeof(string).MakeByRefType() });

                if (instanceField == null || registerMethod == null || tryDequeueMethod == null)
                {
                    continue;
                }

                _registry = instanceField.GetValue(null);
                _registerMethod = registerMethod;
                _tryDequeueMethod = tryDequeueMethod;
                return _registry != null;
            }

            if (!_loggedMissingBroker)
            {
                _loggedMissingBroker = true;
                JumpKing.Program.crashLog.AddErrorMessage("KeyedSaveStates: JumpKingHttpCommandBroker is not loaded.");
            }

            return false;
        }
    }
}