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
            CountBindings(action, out int gamepadCount, out int keyboardCount);

            bool valid =
                (gamepadCount == 1 && keyboardCount == 1)
                || (gamepadCount == 2 && keyboardCount == 2);

            if (!valid)
            {
                errors.Add(
                    $"- {action.name}: Gamepad={gamepadCount}, Keyboard={keyboardCount} (expected 1+1 or 2+2)"
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

    private static void CountBindings(InputAction action, out int gamepadCount, out int keyboardCount)
    {
        gamepadCount = 0;
        keyboardCount = 0;

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

                int partIndex = i + 1;
                while (partIndex < bindings.Count && bindings[partIndex].isPartOfComposite)
                {
                    InputBinding part = bindings[partIndex];
                    compositeGamepad |= IsGamepadBinding(part);
                    compositeKeyboard |= IsKeyboardBinding(part);
                    partIndex++;
                }

                if (compositeGamepad)
                    gamepadCount++;
                if (compositeKeyboard)
                    keyboardCount++;

                continue;
            }

            if (IsGamepadBinding(binding))
                gamepadCount++;
            if (IsKeyboardBinding(binding))
                keyboardCount++;
        }
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
