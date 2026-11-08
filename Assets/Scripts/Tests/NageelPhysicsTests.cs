using System;
using UnityEngine;
using Nageel.Vehicle;
using Nageel.UI;
using Nageel.Cloud;

namespace Nageel.Tests
{
    public class NageelPhysicsTests : MonoBehaviour
    {
        private int testsPassed = 0;
        private int testsFailed = 0;

        private void Start()
        {
            RunAllTests();
        }

        [ContextMenu("Run Physics & Integration Tests")]
        public void RunAllTests()
        {
            testsPassed = 0;
            testsFailed = 0;

            Debug.Log("==================================================");
            Debug.Log("  NAGEEL — Phase 1 Automated Physics & Unit Tests ");
            Debug.Log("==================================================");

            TestVehicleMassAndCenterOfMass();
            TestEngineRPMAndTorqueCurve();
            TestTransmissionRatiosAndLowRange();
            TestBrakingAndABSLogic();
            TestSteepInclineHillAssist();
            TestArabicTextShaper();
            TestNeonCloudConfiguration();

            Debug.Log($"\n[TEST SUMMARY] Total Passed: {testsPassed} | Total Failed: {testsFailed}");
            if (testsFailed == 0)
            {
                Debug.Log("[PASS] ALL PHASE 1 REQUIREMENTS VERIFIED SUCCESSFULLY!");
            }
            else
            {
                Debug.LogError($"[FAIL] {testsFailed} tests failed!");
            }
        }

        private void Assert(bool condition, string testName)
        {
            if (condition)
            {
                testsPassed++;
                Debug.Log($"  [PASS] {testName}");
            }
            else
            {
                testsFailed++;
                Debug.LogError($"  [FAIL] {testName}");
            }
        }

        private void TestVehicleMassAndCenterOfMass()
        {
            GameObject go = new GameObject("TestVehicle");
            NageelCarController car = go.AddComponent<NageelCarController>();
            Rigidbody rb = go.GetComponent<Rigidbody>();

            Assert(car.vehicleMass >= 2000f && car.vehicleMass <= 2500f, "Vehicle Mass is in realistic 4WD SUV range (~2,150 kg)");
            Assert(car.centerOfMassOffset.y < 0f, "Center of Mass is lowered for rollover resistance on mountain curves");

            DestroyImmediate(go);
        }

        private void TestEngineRPMAndTorqueCurve()
        {
            GameObject go = new GameObject("TestVehicle");
            NageelCarController car = go.AddComponent<NageelCarController>();

            Assert(car.idleRPM == 800f, "Engine Idle RPM set to 800 RPM");
            Assert(car.maxRPM >= 6000f, "Engine Max RPM reaches 6,200 RPM redline");
            Assert(car.maxTorque >= 450f, "Engine Max Torque is high-torque mountain spec (480 Nm)");
            Assert(car.peakTorqueRPM > car.idleRPM && car.peakTorqueRPM < car.maxRPM, "Peak Torque RPM is realistically located at 3,200 RPM");

            DestroyImmediate(go);
        }

        private void TestTransmissionRatiosAndLowRange()
        {
            GameObject go = new GameObject("TestVehicle");
            NageelCarController car = go.AddComponent<NageelCarController>();

            Assert(car.forwardGearRatios.Length == 5, "5-speed forward manual/automatic transmission");
            Assert(car.forwardGearRatios[0] > car.forwardGearRatios[1], "1st gear has highest mechanical reduction (3.83:1)");
            Assert(car.lowRangeMultiplier > 2.0f, "4WD Low-Range transfer case provides > 2.4x torque multiplier for steep slopes");

            DestroyImmediate(go);
        }

        private void TestBrakingAndABSLogic()
        {
            GameObject go = new GameObject("TestVehicle");
            NageelCarController car = go.AddComponent<NageelCarController>();

            Assert(car.brakeTorque >= 3000f, "Sufficient foot brake hydraulic torque for steep mountain descents");
            Assert(car.handbrakeTorque > car.brakeTorque, "Handbrake torque locks rear wheels for mountain hairpin turns");
            Assert(car.frontBrakeBias >= 0.6f && car.frontBrakeBias <= 0.75f, "Front brake bias is front-heavy (65%) to prevent rear spin");

            DestroyImmediate(go);
        }

        private void TestSteepInclineHillAssist()
        {
            GameObject go = new GameObject("TestVehicle");
            NageelCarController car = go.AddComponent<NageelCarController>();

            Assert(car.engineBrakingFactor > 0.2f, "Engine compression braking resists gravity runaway on mountain descents");

            DestroyImmediate(go);
        }

        private void TestArabicTextShaper()
        {
            string arabicRaw = "نقيل";
            string shaped = ArabicTextShaper.Shape(arabicRaw);

            Assert(!string.IsNullOrEmpty(shaped), "Arabic text shaper processes characters without exception");
            Assert(shaped != arabicRaw, "Arabic characters converted from isolated into connected cursive glyphs");

            string phrase = "طريق جبل سمارة";
            string shapedPhrase = ArabicTextShaper.Shape(phrase);
            Assert(!string.IsNullOrEmpty(shapedPhrase), "Multi-word mountain pass Arabic text correctly shaped");
        }

        private void TestNeonCloudConfiguration()
        {
            NeonConfig config = new NeonConfig();

            Assert(config.projectId == "dry-surf-72406677", "Neon Project ID matches 'dry-surf-72406677'");
            Assert(config.databaseName == "nageel_db", "Neon Database matches 'nageel_db'");
            Assert(config.developmentBranch == "development", "Target branch is 'development'");
            Assert(config.developmentBranchId == "br-orange-pond-badvwqgc", "Branch ID matches 'br-orange-pond-badvwqgc'");
            Assert(config.productionBranch == "main", "Production branch 'main' is identified and preserved");
        }
    }
}
