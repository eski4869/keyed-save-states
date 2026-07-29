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

        private delegate PlayerEntity ResolvePlayerDelegate(string user);

        private static int _lastResolveAssemblyCount = -1;
        private static ResolvePlayerDelegate _resolvePlayer;

        public static void ResolveApi()
        {
            if (_resolvePlayer != null)
            {
                return;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            if (_lastResolveAssemblyCount == assemblies.Length)
            {
                return;
            }

            _lastResolveAssemblyCount = assemblies.Length;
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type apiType = assemblies[i].GetType(ApiTypeName, false);
                if (apiType == null)
                {
                    continue;
                }

                MethodInfo method = apiType.GetMethod(
                    "ResolvePlayer",
                    BindingFlags.Public | BindingFlags.Static
                );
                _resolvePlayer = method == null ? null :
                    Delegate.CreateDelegate(
                        typeof(ResolvePlayerDelegate),
                        method,
                        false
                    ) as ResolvePlayerDelegate;
                return;
            }
        }

        public static PlayerEntity Resolve(string user)
        {
            ResolveApi();
            if (_resolvePlayer == null)
            {
                return EntityManager.instance == null ? null :
                    EntityManager.instance.Find<PlayerEntity>();
            }

            return _resolvePlayer(user);
        }

        public static bool IsPrimary(PlayerEntity player)
        {
            PlayerEntity primary = EntityManager.instance == null ? null :
                EntityManager.instance.Find<PlayerEntity>();
            return player != null && ReferenceEquals(player, primary);
        }
    }
}
