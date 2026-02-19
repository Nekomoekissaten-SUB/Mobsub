using static Mobsub.Tools.AegisubDialogDslGen.DialogDsl;

// AegisubDialogDslGen C# DSL example.
// The script must return a DialogDef.

Dialog(
    Row(
        Label("Hydra (generated)", w: 7),
        Label("Special:"),
        DropDown("special", new[] { "sort_tags", "convert_clip" }, w: 2)),
    Row(
        CheckBox("c_on", @"\c"),
        ColorAlpha("c"),
        Spacer(1),
        CheckBox("alpha_on", @"\alpha"),
        DropDown("alpha", new[] { "00", "80", "FF" })),
    LeftRight(
        left: new DialogCell[] { Label("Left group", w: 3) },
        right: new DialogCell[] { Label("Right:"), Edit("value", "abc", w: 3) },
        gap: 1)
)
