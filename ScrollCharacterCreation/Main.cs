using HarmonyLib;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace ScrollCharacterCreation;

public class Main : ModSystem {
    private const string ID = "kathanon.scrollcharactercreation";

    private Harmony harmony;

    public override void StartPre(ICoreAPI api) 
        => (harmony ??= new Harmony(ID)).PatchAll();

    public override void Dispose() 
        => harmony?.UnpatchAll(ID);

    public override void StartClientSide(ICoreClientAPI api) {
        Patches.Init(api);
    }
}
