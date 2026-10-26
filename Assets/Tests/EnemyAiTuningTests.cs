using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Warmask.Tests
{
    /// <summary>
    /// EnemyAi lives in Assembly-CSharp, which the test assemblies cannot reference,
    /// so the component is created and read through reflection.
    /// </summary>
    public class EnemyAiTuningTests
    {
        private static readonly Type AiType = Type.GetType("EnemyAi, Assembly-CSharp");

        [TearDown]
        public void ResetEasyMode()
        {
            Globals.Instance.EasyMode = false;
        }

        [Test]
        public void EasyMode_CloneDecidesAtTheSamePaceAsTheOriginal()
        {
            Assert.IsNotNull(AiType, "EnemyAi not found in Assembly-CSharp");

            Globals.Instance.EasyMode = true;

            var original = new GameObject("AI");
            original.AddComponent(AiType);
            var clone = UnityEngine.Object.Instantiate(original);

            Assert.AreEqual(1f * 2.666f, EffectiveInterval(original), 0.01f,
                "Easy Mode should stretch the authored interval");
            Assert.AreEqual(EffectiveInterval(original), EffectiveInterval(clone), 0.01f,
                "A cloned AI must not apply Easy Mode twice");

            UnityEngine.Object.DestroyImmediate(original);
            UnityEngine.Object.DestroyImmediate(clone);
        }

        private static float EffectiveInterval(GameObject ai)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo effective = AiType.GetField("effectiveDecisionInterval", Flags);
            FieldInfo authored = AiType.GetField("decisionInterval", Flags);
            Component component = ai.GetComponent(AiType);

            return (float)(effective != null ? effective.GetValue(component) : authored.GetValue(component));
        }
    }
}
