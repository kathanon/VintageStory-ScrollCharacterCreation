using Cairo;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.GameContent;

namespace ScrollCharacterCreation;
[HarmonyPatch]
public static class Patches {
    private static ICoreClientAPI api;
    private static double topY;
    private static double leftHeight;
    private static double rightHeight;
    private static double scrollHeight;
    private static ScrolledBounds leftScroll;
    private static ScrolledBounds rightScroll;
    private static ScrolledBounds curScroll;
    private static readonly Dictionary<GuiElement, GuiElement[]> surround = new();
    private static readonly Stack<GuiElement[]> surroundStack = new();
    private static readonly List<int> currentSurround = new();

    public static void Init(ICoreClientAPI capi) {
        api = capi;
    }


    [HarmonyPostfix]
    [HarmonyPatch(typeof(GuiComposer), nameof(GuiComposer.Compose))]
    public static void Composer_Compose(
            Dictionary<string, GuiElement> ___interactiveElements,
            List<GuiElement> ___interactiveElementsInDrawOrder) {
        surround.Clear();
        currentSurround.Clear();
        surroundStack.Clear();
        var elems = ___interactiveElements.Values.ToArray();
        var drawOrder = ___interactiveElementsInDrawOrder;

        GuiElement[] array = null;
        for (int i = 0; i < elems.Length; i++) {
            var elem = elems[i];
            if (elem.GetType().FullName == "Vintagestory.API.Client.GuiElementClip") {
                if (Traverse.Create(elem).Field<bool>("clip").Value) {
                    currentSurround.Add(i);
                    surroundStack.Push(array);
                    int n = currentSurround.Count;
                    array = new GuiElement[2 * n];
                    for (int j = 0; j < n; j++) {
                        array[j] = elems[currentSurround[j]];
                    }
                } else if (surroundStack.Count > 0) {
                    array[^currentSurround.Count] = elem;
                    currentSurround.RemoveAt(currentSurround.Count - 1);
                    array = surroundStack.Pop();
                }
            } else if (elem.DrawOrder > 0.0) {
                surround[elem] = array;
            }
        }

        int mid = 0;
        for (int low = 0, high = drawOrder.Count - 1; low < high; ) {
            mid = (low + high) / 2;
            if (drawOrder[mid].DrawOrder > 0.0) {
                high = mid;
            } else {
                low = mid + 1;
            }
        }
        for (int i = mid; i < drawOrder.Count; i++) {
            if (surround.TryGetValue(drawOrder[i], out var arr) && arr != null) {
                int last = i;
                for (int j = 0; j < drawOrder.Count; j++) {
                    if (surround.TryGetValue(drawOrder[j], out var arr2) && arr2 == arr) {
                        last = j;
                    } else {
                        break;
                    }
                }
                int n = arr.Length / 2;
                for (int j = 0; j < n; j++) {
                    drawOrder.Insert(i++, arr[j]);
                }
                i = last + n;
                for (int j = 0; j < n; j++) {
                    drawOrder.Insert(++i, arr[n + j]);
                }
            }
        }

        surround.Clear();
        currentSurround.Clear();
        surroundStack.Clear();
    }

    [HarmonyPostfix]
    [HarmonyPatch("Vintagestory.API.Client.GuiElementClip", "ComposeElements")]
    public static void Clip_Compose(Context ctxStatic, bool ___clip, ElementBounds ___Bounds) {
        if (___clip) {
            var b = ___Bounds;
            double x1 = b.drawX;
            double y1 = b.drawY;
            double x2 = x1 + b.OuterWidth;
            double y2 = y1 + b.OuterHeight;

            ctxStatic.Save();
            ctxStatic.MoveTo(x1, y1);
            ctxStatic.LineTo(x1, y2);
            ctxStatic.LineTo(x2, y2);
            ctxStatic.LineTo(x2, y1);
            ctxStatic.ClosePath();
            ctxStatic.Clip();
        } else {
            ctxStatic.Restore();
        }
    }


    [HarmonyTranspiler]
    [HarmonyPatch(typeof(GuiDialogCreateCharacter), "ComposeGuis")]
    public static IEnumerable<CodeInstruction> CreateCharacter_Compose_Trans(IEnumerable<CodeInstruction> instructions) {
        var colbreak = AccessTools.Field(typeof(SkinnablePart), nameof(SkinnablePart.Colbreak));
        bool foundColBreak = false;
        bool atLoopStart = false;

        foreach (var instr in instructions) {
            if (instr.opcode == OpCodes.Ret) {
                yield return CodeInstruction.Call(typeof(Patches), nameof(Composed));
            }

            yield return instr;

            if (instr.opcode == OpCodes.Stloc_S 
                && instr.operand is LocalVariableInfo v1 
                && v1.LocalIndex == 18) {
                yield return new(OpCodes.Ldloc_S, 7);
                yield return new(OpCodes.Ldloc_S, 18);
                yield return new(OpCodes.Ldloc_S, 4);
                yield return new(OpCodes.Ldloc_S, 13);
                yield return new(OpCodes.Ldloc_S, 14);
                yield return new(OpCodes.Ldarg_0);
                yield return CodeInstruction.LoadField(typeof(GuiDialogCreateCharacter), "insetSlotBounds");
                yield return new(OpCodes.Ldloca_S, 17);
                yield return CodeInstruction.Call(typeof(Patches), nameof(PreLoop));
                atLoopStart = true;
            } else if (atLoopStart
                    && instr.opcode == OpCodes.Stloc_S 
                    && instr.operand is LocalVariableInfo v2 
                    && v2.LocalIndex == 15) {
                yield return new(OpCodes.Ldloc_S, 15);
                yield return CodeInstruction.Call(typeof(Patches), nameof(NewBounds));
                atLoopStart = false;
            } else if (instr.LoadsField(colbreak)) {
                foundColBreak = true;
            } else if (foundColBreak 
                    && instr.opcode == OpCodes.Stloc_S 
                    && instr.operand is LocalVariableInfo v3 
                    && v3.LocalIndex == 17) {
                yield return new(OpCodes.Ldloc_S, 7);
                yield return new(OpCodes.Ldloc_S, 4);
                yield return new(OpCodes.Ldloc_S, 15);
                yield return new(OpCodes.Ldloca_S, 17);
                yield return CodeInstruction.Call(typeof(Patches), nameof(ColBreak));
            } else if (foundColBreak 
                    && instr.opcode == OpCodes.Blt) {
                yield return new(OpCodes.Ldloc_S, 7);
                yield return new(OpCodes.Ldloc_S, 15);
                yield return CodeInstruction.Call(typeof(Patches), nameof(PostLoop));
            }
        }
    }

    public static void PreLoop(GuiComposer composer,
                               SkinnablePart[] parts,
                               ElementBounds dialog,
                               ElementBounds left, 
                               ElementBounds inset,
                               ElementBounds button,
                               ref double leftX) { 
        int colorColumns = 180 / 27 + 1;
        int remove = 18;
        int height = 0;
        foreach (var part in parts) {
            if (part.Type == EnumSkinnableType.Texture && !part.UseDropDown) {
                int rows = (part.Variants.Length - 1) / colorColumns + 1;
                height += rows * 27 + 35;
            } else {
                height += 62;
            }

            if (part.Colbreak) {
                leftHeight = height - remove;
                height = 0;
            }
        }
        rightHeight = height - remove;
        scrollHeight = left.fixedHeight;
        topY = left.fixedY;

        rightScroll = null;
        double leftSpace = 0.0, rightSpace = 0.0;
        if (leftHeight > scrollHeight) {
            leftSpace = 26.0;
            inset.fixedX += leftSpace;
            button.fixedX += leftSpace;

            curScroll = leftScroll = new(left.fixedX, topY, left.fixedWidth, leftHeight, scrollHeight);
            leftScroll.Outer.ParentBounds = dialog;
            leftScroll.BeginScroll(composer, "leftScroll");

            leftX += 1.0;
        }

        if (rightHeight > scrollHeight) {
            rightSpace = 30.0;
        }

        double widen = leftSpace + rightSpace;
        dialog.fixedWidth += widen;
        dialog.ParentBounds.fixedWidth += widen;
    }

    public static void NewBounds(ElementBounds bounds) {
        if (curScroll != null) {
            if (bounds.fixedY == -10.0) bounds.fixedY = -32.0;
            curScroll.Inner.WithChild(bounds);
        }
    }

    public static void ColBreak(GuiComposer composer,
                                ElementBounds dialog,
                                ElementBounds elemBound,
                                ref double leftX) { 
        if (leftHeight > scrollHeight) {
            leftScroll.EndScroll(composer);
            curScroll = null;
        }
        if (rightHeight > scrollHeight) {
            curScroll = rightScroll = new(leftX, topY, dialog.fixedWidth - leftX - 28.0, rightHeight, scrollHeight);
            rightScroll.Outer.ParentBounds = dialog;
            rightScroll.BeginScroll(composer, "rightScroll");
            leftX = 1.0;
        }
    }

    public static void PostLoop(GuiComposer composer,
                                ElementBounds elemBound) { 
        if (rightHeight > scrollHeight) {
            rightScroll.EndScroll(composer);
            curScroll = null;
        }
    }

    public static void Composed() { 
        leftScroll?.SetupScrollbar(false);
        rightScroll?.SetupScrollbar();

        leftScroll = null;
        rightScroll = null;
    }
}
