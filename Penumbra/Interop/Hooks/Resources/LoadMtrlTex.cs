using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using OtterGui.Services;
using Penumbra.GameData;

namespace Penumbra.Interop.Hooks.Resources;

public sealed unsafe class LoadMtrlTex : FastHook<LoadMtrlTex.Delegate>
{
    private readonly GameState _gameState;

    public LoadMtrlTex(HookManager hooks, GameState gameState)
    {
        _gameState = gameState;
        Task       = hooks.CreateHook<Delegate>("Load Material Textures", Sigs.LoadMtrlTex, Detour, HookSettings.ResourceHooks);
    }

    public delegate byte Delegate(MaterialResourceHandle* mtrlResourceHandle);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private byte Detour(MaterialResourceHandle* handle)
    {
        var last = _gameState.MtrlData.Value;
        _gameState.MtrlData.Value = _gameState.LoadSubFileHelper((nint)handle);
        var ret = Task.Result.Original(handle);
        _gameState.MtrlData.Value = last;
        return ret;
    }
}
