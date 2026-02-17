using nadena.dev.modular_avatar.core;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public class Parameter
    {
        public string name = "";
        public AnimatorControllerParameterType parameterType;

        public float defaultFloat = 0;
        public int defaultInt = 0;
        public bool defaultBool = false;

        public bool saved = false;
        public bool localOnly = false;

        public Parameter Clone()
        {
            return new Parameter
            {
                name = name,
                parameterType = parameterType,
                defaultBool = defaultBool,
                defaultFloat = defaultFloat,
                defaultInt = defaultInt,
                saved = saved,
                localOnly = localOnly,
            };
        }

        public Parameter OverrideDefaultBool(bool defaultBool)
        {
            var p = Clone();
            p.defaultBool = defaultBool;
            return p;
        }

        public Parameter OverrideDefaultFloat(float defaultFloat)
        {
            var p = Clone();
            p.defaultFloat = defaultFloat;
            return p;
        }

        public Parameter OverrideDefaultInt(int defaultInt)
        {
            var p = Clone();
            p.defaultInt = defaultInt;
            return p;
        }

        public AnimatorControllerParameter ToAnimatorControllerParameter()
        {
            var acp = new AnimatorControllerParameter
            {
                name = name,
                type = parameterType
            };

            switch (parameterType)
            {
                case AnimatorControllerParameterType.Bool:
                    acp.defaultBool = defaultBool;
                    break;
                case AnimatorControllerParameterType.Float:
                    acp.defaultFloat = defaultFloat;
                    break;
                case AnimatorControllerParameterType.Int:
                    acp.defaultInt = defaultInt;
                    break;
                case AnimatorControllerParameterType.Trigger:
                    acp.defaultBool = defaultBool;
                    break;
            }

            return acp;
        }

        public ParameterConfig ToParameterConfig()
        {
            var pc = new ParameterConfig
            {
                nameOrPrefix = name,
                saved = saved,
                localOnly = localOnly,
            };

            switch (parameterType)
            {
                case AnimatorControllerParameterType.Bool:
                    pc.syncType = ParameterSyncType.Bool;
                    break;
                case AnimatorControllerParameterType.Float:
                    pc.syncType = ParameterSyncType.Float;
                    break;
                case AnimatorControllerParameterType.Int:
                    pc.syncType = ParameterSyncType.Int;
                    break;
                case AnimatorControllerParameterType.Trigger:
                    pc.syncType = ParameterSyncType.Bool;
                    break;
            }

            switch (pc.syncType)
            {
                case ParameterSyncType.Bool:
                    pc.defaultValue = defaultBool ? 1f : 0f;
                    break;
                case ParameterSyncType.Float:
                    pc.defaultValue = defaultFloat;
                    break;
                case ParameterSyncType.Int:
                    pc.defaultValue = (int)defaultFloat;
                    break;
            }

            return pc;
        }
    }
}