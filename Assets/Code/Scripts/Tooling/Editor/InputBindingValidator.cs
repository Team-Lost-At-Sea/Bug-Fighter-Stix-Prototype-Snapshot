using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

[InitializeOnLoad]
public static class InputBindingValidator
{
    private const string INPUT_ACTIONS_PATH = "Assets/Code/Scripts/GameLoop/InputSystem/InputSystem_Actions.inputactions";
    private const string DEFAULT_PROJECT_CONFIG_PATH = "Assets/Code/Data/ProjectConfig.asset";
    private const string GAMEPLAY_MAP_NAME = "Gameplay";
    private const string GAMEPAD_GROUP = "Gamepad";
    private const string KEYBOARD_GROUP = "Keyboard&Mouse";
    private static string cachedProjectConfigPath = DEFAULT_PROJECT_CONFIG_PATH;

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

        ProjectConfig projectConfig = LoadProjectConfig();
        if (projectConfig == null)
            return;

        InputBindingPolicy policyAsset = projectConfig.inputBindingPolicy;
        if (policyAsset == null)
        {
            Debug.LogError(
                $"INPUT BINDING POLICY MISSING\nConfig asset: {cachedProjectConfigPath}\nField: ProjectConfig.inputBindingPolicy"
            );
            return;
        }

        InputActionMap gameplay = asset.FindActionMap(GAMEPLAY_MAP_NAME, false);
        if (gameplay == null)
            return;

        List<string> errors = new List<string>();
        ValidatePolicyCoverage(policyAsset, gameplay, errors);

        foreach (InputAction action in gameplay.actions)
        {
            if (!policyAsset.TryGetPolicy(GAMEPLAY_MAP_NAME, action.name, out InputBindingPolicy.ActionPolicy policy))
                continue;

            BindingSummary summary = CountBindings(action);
            ValidateActionAgainstPolicy(action.name, policy, summary, errors);
        }

        if (errors.Count == 0)
            return;

        foreach (string error in errors)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(GetPrimaryViolationHeadline(error));
            builder.AppendLine("INPUT BINDING RULE VIOLATION (Gameplay map)");
            builder.AppendLine("Bindings must satisfy the authored InputBindingPolicy limits and required defaults.");
            builder.AppendLine($"Asset: {INPUT_ACTIONS_PATH}");
            builder.AppendLine($"ProjectConfig: {cachedProjectConfigPath}");
            builder.AppendLine($"Policy: {AssetDatabase.GetAssetPath(policyAsset)}");
            builder.AppendLine(error);
            Debug.LogError(builder.ToString());
        }
    }

    private static ProjectConfig LoadProjectConfig()
    {
        ProjectConfig projectConfig = AssetDatabase.LoadAssetAtPath<ProjectConfig>(cachedProjectConfigPath);
        if (projectConfig != null)
            return projectConfig;

        string[] guids = AssetDatabase.FindAssets("t:ProjectConfig");
        if (guids.Length == 1)
        {
            string resolvedPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            projectConfig = AssetDatabase.LoadAssetAtPath<ProjectConfig>(resolvedPath);
            if (projectConfig != null)
            {
                string previousPath = cachedProjectConfigPath;
                cachedProjectConfigPath = resolvedPath;
                Debug.LogWarning(
                    $"ProjectConfig moved or cache was stale. Updated validator cache from '{previousPath}' to '{resolvedPath}'."
                );
                return projectConfig;
            }
        }

        if (guids.Length > 1)
        {
            StringBuilder duplicateBuilder = new StringBuilder();
            duplicateBuilder.AppendLine("MULTIPLE PROJECT CONFIG ASSETS FOUND");
            duplicateBuilder.AppendLine($"Cached path: {cachedProjectConfigPath}");
            duplicateBuilder.AppendLine($"Default path: {DEFAULT_PROJECT_CONFIG_PATH}");
            duplicateBuilder.AppendLine("The validator requires exactly one authoritative ProjectConfig asset.");
            duplicateBuilder.AppendLine("Resolve duplicate configs in the project before continuing.");
            foreach (string guid in guids)
                duplicateBuilder.AppendLine($" - {AssetDatabase.GUIDToAssetPath(guid)}");

            Debug.LogError(duplicateBuilder.ToString());
            return null;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("PROJECT CONFIG MISSING");
        builder.AppendLine($"Cached path: {cachedProjectConfigPath}");
        builder.AppendLine($"Default path: {DEFAULT_PROJECT_CONFIG_PATH}");
        if (guids.Length == 0)
        {
            builder.AppendLine("Recovery search found 0 ProjectConfig assets.");
            builder.AppendLine("Create or restore a ProjectConfig asset and assign its Input Binding Policy reference.");
        }

        Debug.LogError(builder.ToString());
        return null;
    }

    private static void ValidatePolicyCoverage(
        InputBindingPolicy policyAsset,
        InputActionMap gameplay,
        List<string> errors
    )
    {
        HashSet<string> seenPolicies = new HashSet<string>();
        for (int i = 0; i < policyAsset.actions.Count; i++)
        {
            InputBindingPolicy.ActionPolicy policy = policyAsset.actions[i];
            if (policy == null)
            {
                errors.Add($"- Policy entry [{i}] is null.");
                continue;
            }

            string policyKey = $"{policy.actionMapName}/{policy.actionName}";
            if (!seenPolicies.Add(policyKey))
                errors.Add($"- Duplicate policy entry for '{policyKey}'.");

            if (!string.Equals(policy.actionMapName, GAMEPLAY_MAP_NAME))
                errors.Add($"- Policy entry '{policyKey}' targets unsupported map '{policy.actionMapName}'.");
        }

        for (int i = 0; i < gameplay.actions.Count; i++)
        {
            InputAction action = gameplay.actions[i];
            if (!policyAsset.TryGetPolicy(GAMEPLAY_MAP_NAME, action.name, out _))
                errors.Add($"- {action.name}: Missing policy entry for map '{GAMEPLAY_MAP_NAME}'.");
        }
    }

    private static void ValidateActionAgainstPolicy(
        string actionName,
        InputBindingPolicy.ActionPolicy policy,
        BindingSummary summary,
        List<string> errors
    )
    {
        List<string> actionErrors = new List<string>();

        ValidateDeviceFamily(
            actionErrors,
            "Gamepad",
            summary.gamepad,
            policy.GetMaxBindings(InputBindingDeviceFamily.Gamepad),
            policy.RequiresDefaultBinding(InputBindingDeviceFamily.Gamepad)
        );
        ValidateDeviceFamily(
            actionErrors,
            "Keyboard",
            summary.keyboard,
            policy.GetMaxBindings(InputBindingDeviceFamily.Keyboard),
            policy.RequiresDefaultBinding(InputBindingDeviceFamily.Keyboard)
        );

        if (actionErrors.Count == 0)
            return;

        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"- {actionName}:");
        builder.AppendLine(
            $"    Policy: Gamepad max={policy.maxGamepadBindings}, requireDefault={policy.requireDefaultGamepadBinding}, runtimeUnbind={policy.allowRuntimeGamepadUnbind}; " +
            $"Keyboard max={policy.maxKeyboardBindings}, requireDefault={policy.requireDefaultKeyboardBinding}, runtimeUnbind={policy.allowRuntimeKeyboardUnbind}"
        );
        AppendActionDiagnosis(builder, summary);
        for (int i = 0; i < actionErrors.Count; i++)
            builder.AppendLine($"    {actionErrors[i]}");

        builder.AppendLine($"    Gamepad slots: total={summary.gamepad.totalCount}, live={summary.gamepad.liveCount}, matches={FormatDetailList(summary.gamepad.details)}");
        builder.AppendLine($"    Keyboard slots: total={summary.keyboard.totalCount}, live={summary.keyboard.liveCount}, matches={FormatDetailList(summary.keyboard.details)}");
        builder.AppendLine($"    Unclassified: {FormatDetailList(summary.unclassifiedBindingDetails)}");
        errors.Add(builder.ToString().TrimEnd());
    }

    private static void ValidateDeviceFamily(
        List<string> errors,
        string deviceFamilyName,
        DeviceBindingSummary summary,
        int maxBindings,
        bool requireDefaultBinding
    )
    {
        if (summary.totalCount > maxBindings)
        {
            errors.Add(
                $"{deviceFamilyName} slots exceed max: found {summary.totalCount}, allowed {maxBindings}. Offending matches: {FormatOverflowDetails(summary.details, maxBindings)}"
            );
        }

        if (requireDefaultBinding && summary.liveCount == 0)
        {
            errors.Add(
                $"{deviceFamilyName} requires at least one live default binding, but found 0 live bindings. Candidate matches: {FormatDetailList(summary.details)}"
            );
        }
    }

    private static BindingSummary CountBindings(InputAction action)
    {
        BindingSummary summary = new BindingSummary();

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
                bool compositeLiveGamepad = false;
                bool compositeLiveKeyboard = false;
                List<string> parts = new List<string>();

                int partIndex = i + 1;
                while (partIndex < bindings.Count && bindings[partIndex].isPartOfComposite)
                {
                    InputBinding part = bindings[partIndex];
                    parts.Add(DescribeBinding(partIndex, part));
                    bool partMatchesGamepad = IsGamepadBinding(part);
                    bool partMatchesKeyboard = IsKeyboardBinding(part);
                    compositeGamepad |= partMatchesGamepad;
                    compositeKeyboard |= partMatchesKeyboard;
                    compositeLiveGamepad |= partMatchesGamepad && HasLivePath(part);
                    compositeLiveKeyboard |= partMatchesKeyboard && HasLivePath(part);
                    partIndex++;
                }

                string compositeHeader = $"[{i}] composite '{binding.name}'";
                string compositeDescription = parts.Count > 0
                    ? $"{compositeHeader} parts={string.Join(", ", parts)}"
                    : $"{compositeHeader} (no parts)";

                if (compositeGamepad)
                {
                    summary.gamepad.totalCount++;
                    if (compositeLiveGamepad)
                        summary.gamepad.liveCount++;
                    summary.gamepad.details.Add(compositeDescription);
                }
                if (compositeKeyboard)
                {
                    summary.keyboard.totalCount++;
                    if (compositeLiveKeyboard)
                        summary.keyboard.liveCount++;
                    summary.keyboard.details.Add(compositeDescription);
                }
                if (!compositeGamepad && !compositeKeyboard)
                    summary.unclassifiedBindingDetails.Add(compositeDescription);

                continue;
            }

            bool matchesGamepad = IsGamepadBinding(binding);
            bool matchesKeyboard = IsKeyboardBinding(binding);
            string detail = DescribeBinding(i, binding);
            if (matchesGamepad)
            {
                summary.gamepad.totalCount++;
                if (HasLivePath(binding))
                    summary.gamepad.liveCount++;
                summary.gamepad.details.Add(detail);
            }
            if (matchesKeyboard)
            {
                summary.keyboard.totalCount++;
                if (HasLivePath(binding))
                    summary.keyboard.liveCount++;
                summary.keyboard.details.Add(detail);
            }
            if (!matchesGamepad && !matchesKeyboard)
                summary.unclassifiedBindingDetails.Add(detail);
        }

        return summary;
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

    private static string FormatOverflowDetails(List<string> details, int allowedCount)
    {
        if (details == null || details.Count == 0)
            return "<none>";

        if (allowedCount < 0)
            allowedCount = 0;

        if (details.Count <= allowedCount)
            return FormatDetailList(details);

        return string.Join(" | ", details.GetRange(allowedCount, details.Count - allowedCount));
    }

    private static void AppendActionDiagnosis(StringBuilder builder, BindingSummary summary)
    {
        List<string> hints = new List<string>();

        if (summary.gamepad.totalCount > 1)
        {
            bool hasStickComposite = ContainsAny(summary.gamepad.details, "<Gamepad>/leftStick/");
            bool hasDpadComposite = ContainsAny(summary.gamepad.details, "<Gamepad>/dpad/");
            if (hasStickComposite && hasDpadComposite)
            {
                hints.Add(
                    "Likely cause: this action has both a left-stick composite and a d-pad composite, so the validator counts 2 gamepad slots."
                );
            }
        }

        if (summary.keyboard.totalCount > 1 && summary.keyboard.liveCount < summary.keyboard.totalCount)
        {
            if (ContainsAny(summary.keyboard.details, "path='<empty>'"))
            {
                hints.Add(
                    "Likely cause: this action has an extra keyboard composite with empty paths. Remove the empty placeholder composite or finish binding it."
                );
            }
        }

        if (summary.unclassifiedBindingDetails.Count > 0)
        {
            hints.Add(
                $"Unclassified bindings are present and may need a proper group/path classification: {FormatDetailList(summary.unclassifiedBindingDetails)}"
            );
        }

        if (hints.Count == 0)
            return;

        for (int i = 0; i < hints.Count; i++)
            builder.AppendLine($"    Diagnosis: {hints[i]}");
    }

    private static bool ContainsAny(List<string> details, string fragment)
    {
        if (details == null || string.IsNullOrEmpty(fragment))
            return false;

        for (int i = 0; i < details.Count; i++)
        {
            string detail = details[i];
            if (!string.IsNullOrEmpty(detail) && detail.Contains(fragment))
                return true;
        }

        return false;
    }

    private static string GetPrimaryViolationHeadline(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return "INPUT BINDING RULE VIOLATION";

        string[] lines = error.Split('\n');
        string actionName = null;
        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (trimmed.StartsWith("- ") && trimmed.EndsWith(":"))
            {
                actionName = trimmed.Substring(2, trimmed.Length - 3);
                continue;
            }

            if (trimmed.StartsWith("Diagnosis: "))
            {
                if (!string.IsNullOrEmpty(actionName))
                    return $"INPUT BINDING RULE VIOLATION: {actionName} - {trimmed.Substring("Diagnosis: ".Length)}";

                return $"INPUT BINDING RULE VIOLATION: {trimmed.Substring("Diagnosis: ".Length)}";
            }

            if (!trimmed.StartsWith("Policy:")
                && !trimmed.StartsWith("Gamepad slots:")
                && !trimmed.StartsWith("Keyboard slots:")
                && !trimmed.StartsWith("Unclassified:")
                && !string.IsNullOrEmpty(trimmed))
            {
                if (!string.IsNullOrEmpty(actionName))
                    return $"INPUT BINDING RULE VIOLATION: {actionName} - {trimmed}";

                return $"INPUT BINDING RULE VIOLATION: {trimmed}";
            }
        }

        return !string.IsNullOrEmpty(actionName)
            ? $"INPUT BINDING RULE VIOLATION: {actionName}"
            : "INPUT BINDING RULE VIOLATION";
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

    private static bool HasLivePath(InputBinding binding)
    {
        return !string.IsNullOrWhiteSpace(binding.path);
    }

    private sealed class BindingSummary
    {
        public readonly DeviceBindingSummary gamepad = new DeviceBindingSummary();
        public readonly DeviceBindingSummary keyboard = new DeviceBindingSummary();
        public readonly List<string> unclassifiedBindingDetails = new List<string>();
    }

    private sealed class DeviceBindingSummary
    {
        public int totalCount;
        public int liveCount;
        public readonly List<string> details = new List<string>();
    }
}
