# Koikatsu Sunshine (KKS)

## Download and install

**[Download GIN_Rope_This_KKS.zip](https://github.com/GoriGinsan/GIN_Rope_This/releases/latest/download/GIN_Rope_This_KKS.zip)**

Close KKS Studio, extract the ZIP, and copy its **BepInEx** folder into the KKS game folder containing **CharaStudio.exe**. Install only one `GIN Rope This.dll`, including copies in plugin subfolders.

The KK and KKS packages contain different DLLs with the same filename. Use the KKS package only in Koikatsu Sunshine. The installation ZIP contains no source files, game files, or bundled dependencies.

## Dependencies

- BepInEx 5 for the KKS game.
- **KKSAPI**, from [Illusion Modding API](https://github.com/IllusionMods/IllusionModdingAPI/releases).
- **KKS ExtensibleSaveFormat**, from [BepisPlugins](https://github.com/IllusionMods/BepisPlugins/releases).
- Optional: the KKS **NodesConstraints** build from [HSPlugins](https://github.com/IllusionMods/HSPlugins/releases).

The tested KKS setup used **Unity 2019.4.9f1**, **BepInEx 5.4.19.0**, **KKSAPI 1.33**, **Extended Save 18.2**, and **NodesConstraints 1.2.2**. These are the tested versions, not claims about the newest available dependencies.

## Controls

The controls are the same as the KK version. Open Studio's **Option** button group, then click **Rope This**. The panel starts closed; click the same entry or press **F8** to toggle it.

Existing supported chains use suggested A/B endpoints. **Option** inside the panel exposes manual bone selection. Static and single-bone items offer **Rope This(Vertical)** and **Rope This(Horizontal)**, with 3–30 intermediate bones and a default of 10.

Move the generated **GIN Rope A/B** handles directly, or attach them yourself using NodesConstraints. Use **Remove rope (restore item; keep handles)** to restore the original item.

## KKS-specific behavior

The shared source is compiled against KKS assemblies and Unity 2019 engine modules. The KKS build uses `QualitySettings.skinWeights` instead of the older KK engine API.

Automatic conversion supports up to four skin influences per vertex. KKS can store more influences; such meshes are refused before conversion so their original mesh and renderer state stay intact. Other existing limits and the [tested scope](testing.md) still apply.

Settings and generated rigs are saved within KKS scenes. Converting an entire scene between KK and KKS is outside the tested scope; sharing a plugin data format does not make game scene files interchangeable.
