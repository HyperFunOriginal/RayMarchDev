using System.Collections;
using System.Collections.Generic;
using Raymarch;
using UnityEngine;

public class SphereSpawnerScript : MonoBehaviour
{
    Render raymarcher;
    // Use this for initialization
    void Start()
    {
        raymarcher = GetRenderer.GetRendererMethod();
        StartCoroutine(SlowUpdate());

        raymarcher.spheresToRender.Add(new SphereObject() { mass = 0.1f, radius = 633f, color = new Color(0.6f, 0.55f, 0.5f), atmosphericProperties = new Vector2(12f, 30f), atmosphereCol = new Color(0.78f, 0.75f, 0.73f), pos = Vector3.down * 500f, ringBounds = new Vector2(1000f, 2000f) });
        raymarcher.spheresToRender.Add(new SphereObject() { mass = 0.0001f, radius = 17f, color = new Color(0.7f, 0.65f, 0.55f), atmosphericProperties = Vector2.zero, pos = Vector3.back * 2000f - Vector3.up * Random.Range(0f, 700f), momentum = Vector3.right * 0.009f });
        raymarcher.spheresToRender.Add(new SphereObject() { mass = 0.00019f, radius = 21f, color = new Color(0.5f, 0.45f, 0.42f), atmosphericProperties = Vector2.zero, pos = Vector3.back * 3000f - Vector3.up * Random.Range(0f, 700f), momentum = Vector3.right * 0.0083f });
        raymarcher.spheresToRender.Add(new SphereObject() { mass = 0.00015f, radius = 20f, color = new Color(0.46f, 0.53f, 0.6f), atmosphericProperties = new Vector2(1f, 0.01f), pos = Vector3.back * 4500f - Vector3.up * Random.Range(0f, 700f), momentum = Vector3.right * 0.0064f });
        raymarcher.spheresToRender.Add(new SphereObject() { mass = 0.00009f, radius = 16f, color = new Color(0.48f, 0.5f, 0.54f), atmosphericProperties = Vector2.zero, pos = Vector3.back * 7000f - Vector3.up * Random.Range(0f, 700f), momentum = Vector3.right * 0.005f });
        raymarcher.spheresToRender.Add(new SphereObject() { mass = 0.00069f, radius = 68f, color = new Color(0.45f, 0.6f, 0.8f), atmosphericProperties = Vector2.one, pos = Vector3.right * 14500f - Vector3.up * Random.Range(0f, 700f), momentum = (Vector3.back + Vector3.up) * 0.000707f, atmosphereCol = new Color(0.45f, 0.6f, 0.8f) });
        raymarcher.spheresToRender.Add(new SphereObject() { mass = 0.00011f, radius = 18f, color = new Color(0.45f, 0.45f, 0.45f), atmosphericProperties = Vector2.zero, pos = raymarcher.spheresToRender[5].pos + Vector3.right * 300f, momentum = (Vector3.up + Vector3.forward * 1.5f) * 0.000707f });
        raymarcher.cameraPosition = new Vector3(-170f, 190f, -1500f);
        raymarcher.simSpeed = 30000f;
        raymarcher.camMovementSpeed = 0.01f;

        //StartCoroutine(SpawnWBlackhole());
    }

    void Update()
    {
        //if (raymarcher.builtinControls && Input.GetKeyDown(KeyCode.B))
        //{
        //	SpawnRandom();
        //}
    }

    void SpawnRandom()
    {
        SphereObject sph = new SphereObject
        {
            mass = Random.Range(0.5f, 3f) * Random.Range(0.75f, 2f) * Random.Range(0.75f, 2f)
        };
        sph.radius = Mathf.Pow(sph.mass, 0.333333f) * 1.5f;
        sph.pos.z = Random.Range(-60f, -20f);
        sph.pos = Math.RotateVector(sph.pos, Random.Range(-4f, 4f), Random.Range(-1f, 1f));

        sph.SetVel(Math.GetPerpendicular(sph.pos).normalized / Mathf.Sqrt(sph.pos.magnitude) * 68f);
        float atmostrength = Mathf.Max(sph.mass - 3f, 0f);
        sph.atmosphericProperties.x = Mathf.Sqrt(atmostrength) * 0.4f;
        sph.atmosphericProperties.y = atmostrength;
        sph.color = Random.ColorHSV(0, 1, 0, 1, 0.3f, 1f);
        sph.atmosphereCol = (Random.ColorHSV() + new Color(0.45f, 0.6f, 0.8f)) / 2f;

        raymarcher.spheresToRender.Add(sph);
    }

    IEnumerator SpawnWBlackhole()
    {
        yield return new WaitForEndOfFrame();

        raymarcher.spheresToRender.Add(new SphereObject() { mass = 2000f, radius = 0f, atmosphericProperties = Vector2.zero });

        while (true)
        {
            yield return new WaitForEndOfFrame();

            while (raymarcher.spheresToRender.Count < 10)
            {
                SpawnRandom();
            }
            yield return new WaitForEndOfFrame();

            for (int i = 0; i < raymarcher.spheresToRender.Count; i++)
            {
                if (raymarcher.spheresToRender[i].pos.magnitude > 600f)
                {
                    raymarcher.spheresToRender.RemoveAt(i);
                    i--;
                }
            }
        }
    }
    // Update is called once per frame
    IEnumerator SlowUpdate()
    {
        //for (float x = 0; x < 10; x++)
        //      {
        //	for (float y = 0; y < 10; y++)
        //	{
        //		raymarcher.spheresToRender.Add(new SphereObject() { mass = 0.01f, color = Color.grey, radius = 1f, pos = new Vector3(x * 5f, 0f, y * 5f), atmosphericProperties = Vector2.zero, momentum = Random.insideUnitSphere * 0.01f });
        //	}
        //      }

        while (true)
        {
            yield return new WaitForEndOfFrame();

            Vector3 avgPos = Vector3.zero;
            Vector3 avgMom = Vector3.zero;
            float totalMass = 0f;
            for (int i = 0; i < raymarcher.spheresToRender.Count; i++)
            {
                avgPos += raymarcher.spheresToRender[i].pos * raymarcher.spheresToRender[i].mass;
                avgMom += raymarcher.spheresToRender[i].momentum * raymarcher.spheresToRender[i].mass;
                totalMass += raymarcher.spheresToRender[i].mass;
            }
            avgMom /= totalMass;
            avgPos /= totalMass;

            for (int i = 0; i < raymarcher.spheresToRender.Count; i++)
            {
                raymarcher.spheresToRender[i].pos -= avgPos;
                raymarcher.spheresToRender[i].momentum -= avgMom;
            }
        }
    }
}
