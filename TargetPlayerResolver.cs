using System;
using System.Reflection;
using EntityComponent;
using JumpKing.Player;

namespace KeyedSaveStates
{
    internal static class TargetPlayerResolver
    {
        private const string ApiTypeName =
            "LocalMultiplayerMod.LocalMultiplayerApi";

        private delegate int ResolvePlayerMaskDelegate(string user);
        private delegate PlayerEntity GetPlayerDelegate(int playerNumber);

        private static bool _resolved;
        private static ResolvePlayerMaskDelegate _resolvePlayerMask;
        private static GetPlayerDelegate _getPlayer;

        public static void ResolveApi()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type apiType = assemblies[i].GetType(ApiTypeName, false);
                if (apiType == null)
                {
                    continue;
                }

                _resolvePlayerMask = CreateDelegate<ResolvePlayerMaskDelegate>(
                    apiType,
                    "ResolvePlayerMask"
                );
                _getPlayer = CreateDelegate<GetPlayerDelegate>(apiType, "GetPlayer");
                return;
            }
        }

        public static PlayerEntity ResolveSingle(string user)
        {
            ResolveApi();
            if (_resolvePlayerMask == null || _getPlayer == null)
            {
                return EntityManager.instance == null ? null :
                    EntityManager.instance.Find<PlayerEntity>();
            }

            int mask = _resolvePlayerMask(user);
            int playerNumber = MaskToSinglePlayerNumber(mask);
            return playerNumber == 0 ? null : _getPlayer(playerNumber);
        }

        public static bool IsPrimary(PlayerEntity player)
        {
            if (player == null)
            {
                return false;
            }

            ResolveApi();
            PlayerEntity primary = _getPlayer == null
                ? (EntityManager.instance == null
                    ? null
                    : EntityManager.instance.Find<PlayerEntity>())
                : _getPlayer(1);
            return ReferenceEquals(player, primary);
        }

        private static int MaskToSinglePlayerNumber(int mask)
        {
            switch (mask)
            {
                case 1:
                    return 1;
                case 2:
                    return 2;
                case 4:
                    return 3;
                case 8:
                    return 4;
                default:
                    return 0;
            }
        }

        private static T CreateDelegate<T>(Type apiType, string methodName)
            where T : class
        {
            MethodInfo method = apiType.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static
            );
            return method == null ? null :
                Delegate.CreateDelegate(typeof(T), method, false) as T;
        }
    }
}
