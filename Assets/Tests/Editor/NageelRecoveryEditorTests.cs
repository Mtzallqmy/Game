using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Nageel.Recovery;
using Nageel.Vehicle;

namespace Nageel.Tests
{
    public sealed class NageelRecoveryEditorTests
    {
        [Test]
        public void RecoveryRoadIsGenuinelyNonFlatAndCurved()
        {
            Vector3 start = RecoveryTouchOverlay.RecoveryRoadPoint(0f);
            Vector3 middle = RecoveryTouchOverlay.RecoveryRoadPoint(900f);
            Vector3 end = RecoveryTouchOverlay.RecoveryRoadPoint(1800f);
            Assert.That(end.y, Is.GreaterThan(start.y + 80f), "The mountain road must climb.");
            Assert.That(Mathf.Abs(middle.x - start.x), Is.GreaterThan(5f), "The route must curve.");
            Assert.That(Vector3.Distance(start,end), Is.GreaterThan(1000f));
        }

        [Test]
        public void GeneratedRecoveryVehicleHasFourPhysicalWheels()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NageelRecoverySUV.prefab");
            Assert.IsNotNull(prefab, "Run NAGEEL > Recovery > Generate Playable 3D Scenes in Unity Editor first.");
            var car = prefab.GetComponent<NageelCarController>();
            Assert.IsNotNull(car);
            Assert.IsNotNull(prefab.GetComponent<Rigidbody>());
            Assert.IsNotNull(prefab.GetComponent<BoxCollider>());
            Assert.IsNotNull(car.frontLeftCollider);
            Assert.IsNotNull(car.frontRightCollider);
            Assert.IsNotNull(car.rearLeftCollider);
            Assert.IsNotNull(car.rearRightCollider);
        }

        [Test]
        public void Generated3DRoadHasActualCollisionMesh()
        {
            const string path = "Assets/Recovery/Generated/RecoveryRoad.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Assert.IsNotNull(mesh, "Recovery road mesh is missing; run scene generator first.");
            Assert.Greater(mesh.vertexCount, 100);
            Assert.Greater(mesh.triangles.Length, 100);
            var drive = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/RecoveryDrive.unity");
            Assert.IsNotNull(drive, "Playable 3D scene must exist.");
        }

        [Test]
        public void GameIdentityRequirementsRemainConfigured()
        {
            Assert.AreEqual("com.moataz.nageel",
                PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android));
            Assert.That((int)PlayerSettings.Android.minSdkVersion, Is.GreaterThanOrEqualTo(26));
            Assert.That(PlayerSettings.Android.targetArchitectures,
                Is.EqualTo(AndroidArchitecture.ARM64));
        }
    }
}
