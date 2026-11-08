using UnityEngine;

namespace Nageel.Environment
{
    [ExecuteInEditMode]
    public class YemeniVillagePropGenerator : MonoBehaviour
    {
        [Header("Building Dimensions")]
        public float baseWidth = 9.0f;
        public float baseDepth = 8.5f;
        public float floorHeight = 3.2f;
        public int numberOfFloors = 4; // Typical 4 to 6 story Yemeni mountain tower

        public Material basaltStoneMaterial;
        public Material clayBrickMaterial;
        public Material qamariyahWindowMaterial;

        private void Start()
        {
            GenerateYemeniTower();
        }

        [ContextMenu("Build Yemeni Stone Tower")]
        public void GenerateYemeniTower()
        {
            // Clear existing children
            while (transform.childCount > 0)
            {
                DestroyImmediate(transform.GetChild(0).gameObject);
            }

            for (int f = 0; f < numberOfFloors; f++)
            {
                CreateFloor(f);
            }
        }

        private void CreateFloor(int floorIndex)
        {
            GameObject floorObj = new GameObject($"Floor_{floorIndex + 1}");
            floorObj.transform.SetParent(transform, false);
            floorObj.transform.localPosition = new Vector3(0f, floorIndex * floorHeight + (floorHeight * 0.5f), 0f);

            // Subtle taper as building rises (traditional Yemeni engineering)
            float taper = 1.0f - (floorIndex * 0.035f);
            floorObj.transform.localScale = new Vector3(baseWidth * taper, floorHeight, baseDepth * taper);

            MeshFilter mf = floorObj.AddComponent<MeshFilter>();
            MeshRenderer mr = floorObj.AddComponent<MeshRenderer>();
            BoxCollider bc = floorObj.AddComponent<BoxCollider>();

            mf.sharedMesh = CreateCubeMesh();

            // Lower two floors are dark mountain basalt stone, upper floors are red baked clay brick
            if (floorIndex < 2)
            {
                if (basaltStoneMaterial != null) mr.sharedMaterial = basaltStoneMaterial;
            }
            else
            {
                if (clayBrickMaterial != null) mr.sharedMaterial = clayBrickMaterial;
            }
        }

        private Mesh CreateCubeMesh()
        {
            GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh mesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(tempCube);
            return mesh;
        }
    }
}
