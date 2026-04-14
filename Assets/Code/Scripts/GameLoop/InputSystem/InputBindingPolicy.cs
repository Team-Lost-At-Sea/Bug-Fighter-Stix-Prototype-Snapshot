using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Fighter/Input Binding Policy")]
public class InputBindingPolicy : ScriptableObject
{
    [Serializable]
    public class ActionPolicy
    {
        public string actionMapName = "Gameplay";
        public string actionName = "";

        [Min(0)]
        public int maxKeyboardBindings = 1;

        [Min(0)]
        public int maxGamepadBindings = 1;

        public bool requireDefaultKeyboardBinding = true;
        public bool requireDefaultGamepadBinding = true;
        public bool allowRuntimeKeyboardUnbind = true;
        public bool allowRuntimeGamepadUnbind = true;

        public bool Matches(string mapName, string candidateActionName)
        {
            return string.Equals(actionMapName, mapName, StringComparison.Ordinal)
                && string.Equals(actionName, candidateActionName, StringComparison.Ordinal);
        }

        public int GetMaxBindings(InputBindingDeviceFamily deviceFamily)
        {
            switch (deviceFamily)
            {
                case InputBindingDeviceFamily.Gamepad:
                    return maxGamepadBindings;
                default:
                    return maxKeyboardBindings;
            }
        }

        public bool RequiresDefaultBinding(InputBindingDeviceFamily deviceFamily)
        {
            switch (deviceFamily)
            {
                case InputBindingDeviceFamily.Gamepad:
                    return requireDefaultGamepadBinding;
                default:
                    return requireDefaultKeyboardBinding;
            }
        }

        public bool AllowsRuntimeUnbind(InputBindingDeviceFamily deviceFamily)
        {
            switch (deviceFamily)
            {
                case InputBindingDeviceFamily.Gamepad:
                    return allowRuntimeGamepadUnbind;
                default:
                    return allowRuntimeKeyboardUnbind;
            }
        }
    }

    public List<ActionPolicy> actions = new List<ActionPolicy>();

    public bool TryGetPolicy(string actionMapName, string actionName, out ActionPolicy policy)
    {
        for (int i = 0; i < actions.Count; i++)
        {
            ActionPolicy candidate = actions[i];
            if (candidate != null && candidate.Matches(actionMapName, actionName))
            {
                policy = candidate;
                return true;
            }
        }

        policy = null;
        return false;
    }

    public void OnValidate()
    {
        for (int i = 0; i < actions.Count; i++)
        {
            ActionPolicy policy = actions[i];
            if (policy == null)
                continue;

            policy.maxKeyboardBindings = Mathf.Max(0, policy.maxKeyboardBindings);
            policy.maxGamepadBindings = Mathf.Max(0, policy.maxGamepadBindings);
        }
    }
}

public enum InputBindingDeviceFamily
{
    Keyboard = 0,
    Gamepad = 1
}
