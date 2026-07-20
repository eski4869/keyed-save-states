using System;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using EntityComponent;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Mods;
using JumpKing.Player;
using Microsoft.Xna.Framework;

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

            if (!KeyedSaveStateStore.IsValidKey(key))
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
                return;
            }

            string levelName;
            if (!LevelNameResolver.TryGetCurrentLevelName(out levelName))
            {
                return;
            }

            KeyedSaveState state = KeyedSaveState.FromPlayer(
                key,
                levelName,
                player
            );
            KeyedSaveStateStore.SaveState(state);
            PlaySaveSound();
        }

        private static void Load(string key)
        {
            PlayerEntity player = EntityManager.instance.Find<PlayerEntity>();
            if (player == null)
            {
                return;
            }

            string levelName;
            if (!LevelNameResolver.TryGetCurrentLevelName(out levelName))
            {
                return;
            }

            KeyedSaveState state;
            if (!KeyedSaveStateStore.TryLoad(key, levelName, out state))
            {
                return;
            }

            player.m_body.Position = state.Position;
            player.m_body.Velocity = Vector2.Zero;
            JumpKing.Camera.UpdateCamera(player.m_body.GetHitbox().Center);
            PlayLoadSound();
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

    internal static class LevelNameResolver
    {
        public static bool TryGetCurrentLevelName(out string levelName)
        {
            levelName = null;

            try
            {
                object contentManager = Game1.instance.contentManager;
                FieldInfo rootField = contentManager.GetType().GetField(
                    "root",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                );
                string root = rootField.GetValue(contentManager) as string;

                if (root == "Content")
                {
                    levelName = "Main Babe";
                    return true;
                }

                FieldInfo levelField = contentManager.GetType().GetField(
                    "level",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                );
                JumpKing.Workshop.Level level =
                    levelField.GetValue(contentManager) as
                    JumpKing.Workshop.Level;

                if (level != null)
                {
                    levelName = level.Name;
                    return !string.IsNullOrWhiteSpace(levelName);
                }

                return false;
            }
            catch (Exception ex)
            {
                JumpKing.Program.crashLog.AddErrorMessage(
                    "KeyedSaveStates level name failed: " + ex.Message
                );
                return false;
            }
        }
    }

    internal static class KeyedSaveStateStore
    {
        private const string StateDirectoryName = "keyed_save_states";
        private static readonly object Sync = new object();
        private static string _stateDirectory;

        public static void EnsureLoaded()
        {
            lock (Sync)
            {
                EnsureStateDirectory();
            }
        }

        public static bool IsValidKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length > 64)
            {
                return false;
            }

            for (int i = 0; i < key.Length; i++)
            {
                char c = key[i];
                if ((c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    c == '_' ||
                    c == '-')
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        public static void SaveState(KeyedSaveState state)
        {
            EnsureLoaded();

            lock (Sync)
            {
                try
                {
                    string path = GetStatePath(state.LevelName, state.Key);
                    var serializer = new XmlSerializer(typeof(KeyedSaveStateXml));
                    var data = KeyedSaveStateXml.FromState(state);

                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    using (var stream = File.Create(path))
                    {
                        serializer.Serialize(stream, data);
                    }
                }
                catch (Exception ex)
                {
                    JumpKing.Program.crashLog.AddErrorMessage("KeyedSaveStates save failed: " + ex.Message);
                }
            }
        }

        public static bool TryLoad(
            string key,
            string levelName,
            out KeyedSaveState state
        )
        {
            state = new KeyedSaveState();
            EnsureLoaded();

            lock (Sync)
            {
                try
                {
                    string path = GetStatePath(levelName, key);
                    if (!File.Exists(path))
                    {
                        return false;
                    }

                    var serializer = new XmlSerializer(typeof(KeyedSaveStateXml));
                    using (var stream = File.OpenRead(path))
                    {
                        var data = (KeyedSaveStateXml)serializer.Deserialize(stream);
                        if (data == null)
                        {
                            return false;
                        }

                        state = data.ToState(key);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    JumpKing.Program.crashLog.AddErrorMessage("KeyedSaveStates load failed: " + ex.Message);
                    return false;
                }
            }
        }

        private static string GetStatePath(string levelName, string key)
        {
            EnsureStateDirectory();
            return Path.Combine(
                _stateDirectory,
                EncodeLevelDirectoryName(levelName),
                key + ".xml"
            );
        }

        private static string EncodeLevelDirectoryName(string levelName)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            var result = new System.Text.StringBuilder();
            int lastNonTrailing = levelName.TrimEnd(' ', '.').Length;

            for (int i = 0; i < levelName.Length; i++)
            {
                char c = levelName[i];
                bool mustEncode =
                    c == '%' ||
                    Array.IndexOf(invalid, c) >= 0 ||
                    i >= lastNonTrailing;

                if (mustEncode)
                {
                    result.Append('%');
                    result.Append(((int)c).ToString("X4"));
                }
                else
                {
                    result.Append(c);
                }
            }

            string directoryName = result.ToString();
            return string.IsNullOrEmpty(directoryName)
                ? "%0000"
                : directoryName;
        }

        private static void EnsureStateDirectory()
        {
            if (string.IsNullOrEmpty(_stateDirectory))
            {
                string assemblyPath = typeof(ModEntry).Assembly.Location;
                string directory = Path.GetDirectoryName(assemblyPath);
                _stateDirectory = Path.Combine(directory, StateDirectoryName);
            }

            Directory.CreateDirectory(_stateDirectory);
        }
    }
    internal struct KeyedSaveState
    {
        public string Key;
        public string LevelName;
        public int Screen;
        public Vector2 Position;

        public static KeyedSaveState FromPlayer(
            string key,
            string levelName,
            PlayerEntity player
        )
        {
            return new KeyedSaveState
            {
                Key = key,
                LevelName = levelName,
                Screen = JumpKing.Camera.CurrentScreen + 1,
                Position = player.m_body.Position
            };
        }

    }

    [XmlRoot("KeyedSaveState")]
    public class KeyedSaveStateXml
    {
        [XmlElement("level_name")]
        public string LevelName { get; set; }

        [XmlElement("screen")]
        public int Screen { get; set; }

        [XmlElement("x")]
        public float X { get; set; }

        [XmlElement("y")]
        public float Y { get; set; }

        internal static KeyedSaveStateXml FromState(KeyedSaveState state)
        {
            return new KeyedSaveStateXml
            {
                LevelName = state.LevelName,
                Screen = state.Screen,
                X = state.Position.X,
                Y = state.Position.Y
            };
        }

        internal KeyedSaveState ToState(string key)
        {
            return new KeyedSaveState
            {
                Key = key,
                LevelName = LevelName,
                Screen = Screen,
                Position = new Vector2(X, Y)
            };
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
