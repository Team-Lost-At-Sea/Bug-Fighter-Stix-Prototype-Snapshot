using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

[InitializeOnLoad]
public static class InputBindingValidator
{
    private const string INPUT_ACTIONS_PATH = "Assets/Code/Scripts/GameLoop/InputSystem/InputSystem_Actions.inputactions";
    private const string GAMEPLAY_MAP_NAME = "Gameplay";
    private const string GAMEPAD_GROUP = "Gamepad";
    private const string KEYBOARD_GROUP = "Keyboard&Mouse";

    static InputBindingValidator()
    {
        EditorApplication.delayCall += Validate;
        EditorApplication.projectChanged += Validate;
    }

    private static void Validate()
    {
        InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(INPUT_ACTIONS_PATH);
        if (asset == null)
            return;

        InputActionMap gameplay = asset.FindActionMap(GAMEPLAY_MAP_NAME, false);
        if (gameplay == null)
            return;

        List<string> errors = new List<string>();
        foreach (InputAction action in gameplay.actions)
        {
            CountBindings(
                action,
                out int gamepadCount,
                out int keyboardCount,
                out List<string> gamepadBindingDetails,
                out List<string> keyboardBindingDetails,
                out List<string> unclassifiedBindingDetails
            );

            bool valid =
                (gamepadCount == 1 && keyboardCount == 1)
                || (gamepadCount == 2 && keyboardCount == 2);

            if (!valid)
            {
                errors.Add(
                    $"- {action.name}: Gamepad={gamepadCount}, Keyboard={keyboardCount} (expected 1+1 or 2+2)\n" +
                    $"    Gamepad matches: {FormatDetailList(gamepadBindingDetails)}\n" +
                    $"    Keyboard matches: {FormatDetailList(keyboardBindingDetails)}\n" +
                    $"    Unclassified: {FormatDetailList(unclassifiedBindingDetails)}"
                );
            }
        }

        if (errors.Count == 0)
            return;

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("INPUT BINDING RULE VIOLATION (Gameplay map)");
        builder.AppendLine("Every action must have exactly 1 Gamepad + 1 Keyboard binding OR 2 Gamepad + 2 Keyboard bindings.");
        builder.AppendLine($"Asset: {INPUT_ACTIONS_PATH}");
        foreach (string error in errors)
            builder.AppendLine(error);

        Debug.LogError(builder.ToString());
    }

    private static void CountBindings(
        InputAction action,
        out int gamepadCount,
        out int keyboardCount,
        out List<string> gamepadBindingDetails,
        out List<string> keyboardBindingDetails,
        out List<string> unclassifiedBindingDetails
    )
    {
        gamepadCount = 0;
        keyboardCount = 0;
        gamepadBindingDetails = new List<string>();
        keyboardBindingDetails = new List<string>();
        unclassifiedBindingDetails = new List<string>();

        IReadOnlyList<InputBinding> bindings = action.bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            InputBinding binding = bindings[i];
            if (binding.isPartOfComposite)
                continue;

            if (binding.isComposite)
            {
                bool compositeGamepad = false;
                bool compositeKeyboard = false;
                List<string> parts = new List<string>();

                int partIndex = i + 1;
                while (partIndex < bindings.Count && bindings[partIndex].isPartOfComposite)
                {
                    InputBinding part = bindings[partIndex];
                    parts.Add(DescribeBinding(partIndex, part));
                    compositeGamepad |= IsGamepadBinding(part);
                    compositeKeyboard |= IsKeyboardBinding(part);
                    partIndex++;
                }

                string compositeHeader = $"[{i}] composite '{binding.name}'";
                string compositeDescription = parts.Count > 0
                    ? $"{compositeHeader} parts={string.Join(", ", parts)}"
                    : $"{compositeHeader} (no parts)";

                if (compositeGamepad)
                {
                    gamepadCount++;
                    gamepadBindingDetails.Add(compositeDescription);
                }
                if (compositeKeyboard)
                {
                    keyboardCount++;
                    keyboardBindingDetails.Add(compositeDescription);
                }
                if (!compositeGamepad && !compositeKeyboard)
                    unclassifiedBindingDetails.Add(compositeDescription);

                continue;
            }

            bool matchesGamepad = IsGamepadBinding(binding);
            bool matchesKeyboard = IsKeyboardBinding(binding);
            string detail = DescribeBinding(i, binding);
            if (matchesGamepad)
            {
                gamepadCount++;
                gamepadBindingDetails.Add(detail);
            }
            if (matchesKeyboard)
            {
                keyboardCount++;
                keyboardBindingDetails.Add(detail);
            }
            if (!matchesGamepad && !matchesKeyboard)
                unclassifiedBindingDetails.Add(detail);
        }
    }

    private static string DescribeBinding(int index, InputBinding binding)
    {
        string path = string.IsNullOrEmpty(binding.path) ? "<empty>" : binding.path;
        string groups = string.IsNullOrEmpty(binding.groups) ? "<none>" : binding.groups;
        return $"[{index}] path='{path}' groups='{groups}'";
    }

    private static string FormatDetailList(List<string> details)
    {
        if (details == null || details.Count == 0)
            return "<none>";

        return string.Join(" | ", details);
    }

    private static bool IsGamepadBinding(InputBinding binding)
    {
        if (HasGroup(binding, GAMEPAD_GROUP))
            return true;

        return HasPathPrefix(binding.path, "<Gamepad>");
    }

    private static bool IsKeyboardBinding(InputBinding binding)
    {
        if (HasGroup(binding, KEYBOARD_GROUP))
            return true;

        return HasPathPrefix(binding.path, "<Keyboard>") || HasPathPrefix(binding.path, "<Mouse>");
    }

    private static bool HasGroup(InputBinding binding, string group)
    {
        if (string.IsNullOrWhiteSpace(binding.groups))
            return false;

        string[] groups = binding.groups.Split(';');
        foreach (string entry in groups)
        {
            if (string.Equals(entry.Trim(), group))
                return true;
        }

        return false;
    }

    private static bool HasPathPrefix(string path, string prefix)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return path.StartsWith(prefix);
    }
}
