using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Reflection;
using System.IO;

namespace Raymarch
{
    public static class GetRenderer
    {
        public static Render GetRendererMethod()
        {
            return GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Render>();
        }
        public static CommandParser GetCommandSystem()
        {
            return GameObject.FindGameObjectWithTag("Command").GetComponent<CommandParser>();
        }
    }
    public static class Math
    {
        public static Vector3 VelFromMom(Vector3 mom)
        {
            float v = mom.sqrMagnitude;
            if (v == 0f)
            {
                return Vector3.zero;
            }
            return mom.normalized * Mathf.Sqrt(v / (1f + v * 0.00173611111f));
        }

        public static float CalculateAcceleration(Vector3 a, Vector3 b, float amass, float bmass)
        {
            float dist = (a - b).magnitude;
            float schwarzschildRadius = (amass + bmass) * 0.002f;

            float force = amass / Mathf.Max(Mathf.Pow(dist - schwarzschildRadius, 2f), 0.001f);
            float force2 = amass / Mathf.Max(Mathf.Pow(dist - schwarzschildRadius, 3f), 0.001f) * 3f;

            return force + force2;
        }

        public static Vector3 GetPerpendicular(Vector3 a)
        {
            return new Vector3(a.z, 0f, -a.x);
        }

        public static float SchwarzschildRadius(float mass)
        {
            return mass * 0.002f;
        }

        public static Vector3 RotateVector(Vector3 input, float theta, float psi)
        {
            Vector3 psiChange = new Vector3(input.x, input.y * Mathf.Cos(psi) - input.z * Mathf.Sin(psi), input.y * Mathf.Sin(psi) + input.z * Mathf.Cos(psi));
            Vector3 thetaChange = new Vector3(psiChange.x * Mathf.Cos(theta) + psiChange.z * Mathf.Sin(theta), psiChange.y, psiChange.z * Mathf.Cos(theta) - psiChange.x * Mathf.Sin(theta));
            return thetaChange;
        }

        public static void SaveScreenshot(RenderTexture screen)
        {
            Texture2D image = new Texture2D(screen.width, screen.height, TextureFormat.RGB24, false);
            var old_rt = RenderTexture.active;
            RenderTexture.active = screen;

            image.ReadPixels(new Rect(0, 0, screen.width, screen.height), 0, 0);
            image.Apply();

            RenderTexture.active = old_rt;

            byte[] pngEncoded = image.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(image);

            if (!Directory.Exists(Application.dataPath + "/Screenshots/"))
            {
                Directory.CreateDirectory(Application.dataPath + "/Screenshots/");
            }
            int screenshotIndex = 0;
            while (File.Exists(Application.dataPath + "/Screenshots/Screenshot_" + screenshotIndex + ".png"))
            {
                screenshotIndex++;
            }
            File.WriteAllBytes(Application.dataPath + "/Screenshots/Screenshot_" + screenshotIndex + ".png", pngEncoded);
        }
    }
    /// <summary>
    /// Attribute for mod to load in runtime.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class RaymarcherMod : Attribute { }

    public class Render : MonoBehaviour
    {
        [Header("Camera Data", order = 1)]
        public Vector3 cameraPosition;
        public Vector2 cameraEulerAngles;
        public float camMovementSpeed;
        internal Vector3 oldCamPos;
        public float FOV;
        public float exposure;

        [Header("Light Data", order = 2)]
        public Vector2 lightDirection;
        //public Vector2 skyboxAngle;
        public Texture2D skyBox;

        [Header("Settings", order = 4)]
        public Vector2 fpsBounds;
        public float sunBrightness;
        public bool BlueshiftInvariant;
        public bool shadowsEnabled;
        public bool drawAtmospheres;
        public bool builtinCamControls;
        public bool builtinControls;
        public bool equirectangularProjection;
        public Action Controller;

        [Header("Simulation Data", order = 3)]
        public bool simEnabled;
        public float simSpeed;

        [Header("Internal Information", order = 0)]
        public ComputeShader Renderer;
        public GameObject command;
        public RenderTexture screenTex;
        internal FPSCounter counter;
        private float renderScale;
        [SerializeField()]
        public List<SphereObject> spheresToRender;
        [SerializeField()]

        public Vector3 rawCamMove;
        public Vector3 camMoveWorldSpace;
        internal Vector3 mousePos;
        public Vector2 mouseMovement;

        void LoadMods()
        {
            if (!Directory.Exists(Application.dataPath + "/Modifiers/"))
                Directory.CreateDirectory(Application.dataPath + "/Modifiers/");

            foreach (string t in Directory.GetFiles(Application.dataPath + "/Modifiers/"))
            {
                if (!t.EndsWith(".dll"))
                    continue;

                Assembly assembly = Assembly.LoadFile(t);

                foreach (Type ty in assembly.GetTypes())
                {
                    RaymarcherMod[] attrs = (RaymarcherMod[])ty.GetCustomAttributes(typeof(RaymarcherMod), false);

                    Debug.Log(ty.FullName);

                    if (attrs.Length <= 0)
                    {
                        Debug.Log(ty.FullName + " is not a mod");

                        continue;
                    }

                    if (typeof(MonoBehaviour).IsAssignableFrom(ty))
                    {
                        Debug.Log("Loaded class: " + ty.Name);
                        GameObject go = new GameObject(ty.Name + ".mod");
                        go.AddComponent(ty);
                    }
                }
            }
        }
        // Use this for initialization
        void Start()
        {
            fpsBounds = new Vector2(40f, 60f);
            renderScale = 1f;
            Controller = Controls;
            oldCamPos = cameraPosition;
            counter = GameObject.Find("Text").GetComponent<FPSCounter>();
            LoadMods();
            StartCoroutine(SlowUpdate());
        }

        IEnumerator SlowUpdate()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(1f);
                fpsBounds.y = Mathf.Max(fpsBounds.y, 30f);
                fpsBounds.x = Mathf.Min(fpsBounds.y - 7f, fpsBounds.x);
                Application.targetFrameRate = (int)fpsBounds.y;
            }
        }
        struct Buffer
        {
            public float x, y, z;
            public float radius, gravity;
            public float r, g, b;
            public float vx, vy, vz;
            public float ax, ay;
            public float ar, ag, ab;
            public float ir, or;
        }
        // Update is called once per frame
        void Update()
        {
            if (counter.fps < fpsBounds.x)
                renderScale = Mathf.Clamp(0.99f * renderScale, 0.2f, 1f);
            if (counter.fps > fpsBounds.y - 5f)
                renderScale = Mathf.Clamp01(renderScale + 0.0022f);

            if (builtinControls)
            {
                MoveCam();
                Controller.Invoke();
                if (Input.GetKeyDown(KeyCode.C))
                    command.SetActive(!command.activeSelf);
            }
            if (simEnabled)
            {
                for (int k = 0; k < 2; k++)
                {
                    foreach (SphereObject s in spheresToRender)
                    {
                        s.GravityUpdate(spheresToRender, simSpeed);
                    }

                    for (int i = 0; i < spheresToRender.Count; i++)
                    {
                        SphereObject s = spheresToRender[i];

                        for (int j = 0; j < spheresToRender.Count; j++)
                        {
                            SphereObject p = spheresToRender[j];
                            float schwarz = (s.mass + p.mass) * 0.00201f;
                            if ((s.pos - p.pos).magnitude < Mathf.Max(s.radius, p.radius, schwarz * 0.5f) - Mathf.Min(s.radius, p.radius) * 0.5f && i != j)
                            {
                                if (p.mass < s.mass * UnityEngine.Random.Range(0.99f, 1.01f))
                                {
                                    Vector3 totalMomentum = (p.momentum * p.mass + s.momentum * s.mass) / (s.mass + p.mass);
                                    s.momentum = totalMomentum;
                                    s.mass += p.mass;

                                    if (s.radius > schwarz)
                                    {
                                        s.atmosphericProperties.y = (s.atmosphericProperties.y * s.radius * s.radius + p.atmosphericProperties.y * p.radius * p.radius) / (s.radius * s.radius);
                                        s.radius = Mathf.Pow(s.radius * s.radius * s.radius + p.radius * p.radius * p.radius, 0.33333333333333333333f);
                                    }
                                    spheresToRender.Remove(p);
                                    j = spheresToRender.Count;
                                    i = spheresToRender.Count;
                                }
                            }
                        }

                        s.UpdatePosition(simSpeed);
                    }
                }
            }
            oldCamPos = (oldCamPos + cameraPosition * 0.1f) / 1.1f;
        }

        void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            foreach (SphereObject s in spheresToRender)
            {
                s.vel = Math.VelFromMom(s.momentum);
            }

            OnGPU();
            Graphics.Blit(screenTex, dest);
        }
        /// <summary>
        /// Renders the screen to the render texture.
        /// </summary>
        /// <param name="width">width of render, leave 0 or empty for game window width</param>
        /// <param name="height">height of render, leave 0 or empty for game window height</param>
        void OnGPU(int width = 0, int height = 0)
        {
            if (width == 0) width = (int)(Screen.width * renderScale);
            if (height == 0) height = (int)(Screen.height * renderScale);

            if (screenTex == null || screenTex.width != width || screenTex.height != height)
            {
                if (screenTex != null)
                    DestroyImmediate(screenTex);

                screenTex = new RenderTexture(width, height, 24)
                {
                    enableRandomWrite = true
                };
                screenTex.Create();
            }

            ComputeBuffer image = new ComputeBuffer(screenTex.width * screenTex.height, sizeof(float) * 4);

            Buffer[] buff = new Buffer[Mathf.Max(spheresToRender.Count, 1)];
            for (int i = 0; i < spheresToRender.Count; i++)
            {
                buff[i] = new Buffer()
                {
                    x = spheresToRender[i].pos.x - cameraPosition.x,
                    y = spheresToRender[i].pos.y - cameraPosition.y,
                    z = spheresToRender[i].pos.z - cameraPosition.z,
                    radius = spheresToRender[i].radius,
                    gravity = spheresToRender[i].mass * 0.002f,
                    r = spheresToRender[i].color.r,
                    g = spheresToRender[i].color.g,
                    b = spheresToRender[i].color.b,
                    vx = spheresToRender[i].vel.x / 8f,
                    vy = spheresToRender[i].vel.y / 8f,
                    vz = spheresToRender[i].vel.z / 8f,
                    ax = spheresToRender[i].atmosphericProperties.x * 0.4f,
                    ay = Mathf.Log(Mathf.Max(0f, spheresToRender[i].atmosphericProperties.y) + 0.00001f, 7.2f),

                    ar = spheresToRender[i].atmosphereCol.r,
                    ag = spheresToRender[i].atmosphereCol.g,
                    ab = spheresToRender[i].atmosphereCol.b,
                    ir = spheresToRender[i].ringBounds.x,
                    or = spheresToRender[i].ringBounds.y,
                };
            }
            if (spheresToRender.Count < 1) buff[0] = new Buffer();

            ComputeBuffer cb = new ComputeBuffer(buff.Length, sizeof(float) * 18);
            cb.SetData(buff);

            Vector3 lightDir = Math.RotateVector(Vector3.back, lightDirection.x, lightDirection.y);
            Vector3 camDir = Math.RotateVector(Vector3.forward, cameraEulerAngles.x, cameraEulerAngles.y);
            Renderer.SetBool("blueshiftEnabled", !BlueshiftInvariant);
            Renderer.SetBool("atmos", drawAtmospheres);
            Renderer.SetFloats("dimensions", screenTex.width * 0.5f, screenTex.height * 0.5f);
            Renderer.SetInts("skyboxDimensions", skyBox.width, skyBox.height);
            Renderer.SetInt("number", spheresToRender.Count);
            Renderer.SetFloats("camAng", cameraEulerAngles.x, cameraEulerAngles.y);
            //Renderer.SetFloats("skyAng", skyboxAngle.x, skyboxAngle.y);
            Vector3 camVel = (cameraPosition - oldCamPos) * 0.225f / simSpeed;
            Renderer.SetFloats("camVel", camVel.x, camVel.y, camVel.z);
            Renderer.SetFloats("lightDir", lightDir.x, lightDir.y, lightDir.z);
            Renderer.SetFloat("normalDim", Mathf.Sqrt(screenTex.width * screenTex.height) * 0.5f / Mathf.Tan(FOV * Mathf.Deg2Rad * 0.5f));
            Renderer.SetFloat("solarLuminosity", sunBrightness);
            Renderer.SetFloat("exposure", exposure);
            Renderer.SetBool("shadows", shadowsEnabled);
            Renderer.SetBool("equirect", equirectangularProjection);

            if (!drawAtmospheres)
            {
                Renderer.SetTexture(Renderer.FindKernel("Plain"), "skybox", skyBox);
                Renderer.SetBuffer(Renderer.FindKernel("Plain"), "Result", image);
                Renderer.SetBuffer(Renderer.FindKernel("Plain"), "sp", cb);
                Renderer.Dispatch(Renderer.FindKernel("Plain"), Mathf.CeilToInt(screenTex.width / 32f), Mathf.CeilToInt(screenTex.width / 32f), 1);
            }
            else
            {
                Renderer.SetTexture(Renderer.FindKernel("Atmo"), "skybox", skyBox);
                Renderer.SetBuffer(Renderer.FindKernel("Atmo"), "Result", image);
                Renderer.SetBuffer(Renderer.FindKernel("Atmo"), "sp", cb);
                Renderer.Dispatch(Renderer.FindKernel("Atmo"), Mathf.CeilToInt(screenTex.width / 32f), Mathf.CeilToInt(screenTex.width / 32f), 1);
            }

            Renderer.SetBuffer(Renderer.FindKernel("WriteToScreen"), "Result", image);
            Renderer.SetTexture(Renderer.FindKernel("WriteToScreen"), "Res", screenTex);
            Renderer.SetFloats("bloomSize", equirectangularProjection ? width / 3000f : width / 1100f, UnityEngine.Random.Range(0f, 0.3f));
            Renderer.Dispatch(Renderer.FindKernel("WriteToScreen"), Mathf.CeilToInt(screenTex.width / 32f), Mathf.CeilToInt(screenTex.width / 32f), 1);

            cb.Dispose();
            image.Dispose();
        }

        /// <summary>
        /// Runs control check (Settings)
        /// </summary>
        public void Controls()
        {
            if (Input.GetKeyDown(KeyCode.X)) drawAtmospheres = !drawAtmospheres;
            if (Input.GetKeyDown(KeyCode.Z)) BlueshiftInvariant = !BlueshiftInvariant;
            if (Input.GetKeyDown(KeyCode.V)) shadowsEnabled = !shadowsEnabled;
            if (Input.GetKey(KeyCode.Minus)) exposure *= 0.97f;
            if (Input.GetKey(KeyCode.Equals)) exposure *= 1.03f;
            if (Input.GetKeyDown(KeyCode.Space)) simEnabled = !simEnabled;
            if (Input.GetKeyDown(KeyCode.Y)) equirectangularProjection = !equirectangularProjection;
            if (Input.GetKey(KeyCode.LeftControl))
            {
                FOV = Mathf.Clamp(FOV * (1f + Input.mouseScrollDelta.y * 0.03f), 0f, 150f);
                if (Input.GetMouseButton(2))
                    FOV = 90f;
            }
            if (Input.GetKeyDown(KeyCode.F12))
            {
                if (equirectangularProjection)
                {
                    OnGPU(8192, 4096);
                }
                else
                {
                    OnGPU(Screen.width * 3, (int)(Screen.width * 1.68750073828f));
                }
                Math.SaveScreenshot(screenTex);
            }
        }
        private void MoveCam()
        {
            rawCamMove = Vector3.zero;
            if (!Input.GetKey(KeyCode.LeftControl))
                camMovementSpeed *= Input.mouseScrollDelta.y * 0.1f + 1f;
            camMovementSpeed = Mathf.Clamp(camMovementSpeed, 0.0005f / simSpeed, 24f);

            Vector2 dt = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
            rawCamMove.x = dt.x;
            rawCamMove.z = dt.y;
            if (Input.GetKey(KeyCode.Q))
            {
                rawCamMove.y--;
            }

            if (Input.GetKey(KeyCode.E))
            {
                rawCamMove.y++;
            }

            camMoveWorldSpace = Math.RotateVector(rawCamMove.normalized * 0.0533333f * camMovementSpeed, cameraEulerAngles.x, cameraEulerAngles.y);

            if (builtinCamControls)
                cameraPosition += camMoveWorldSpace * simSpeed;

            if (Input.GetMouseButtonDown(1))
            {
                mousePos = Input.mousePosition;
            }
            if (Input.GetMouseButton(1))
            {
                Vector3 delta2 = mousePos - Input.mousePosition;

                mouseMovement = new Vector2(-delta2.x, delta2.y) * 0.1f * Time.deltaTime;
                if (builtinCamControls)
                    cameraEulerAngles += mouseMovement * Mathf.Sqrt(FOV / 90f);

                mousePos = Input.mousePosition;
            }
            else
            {
                mouseMovement = Vector2.zero;
            }
        }
    }

    [Serializable]
    public class SphereObject
    {
        public Vector3 pos = Vector3.zero;
        public float radius = 1f;
        public float mass = 1f;
        public Color color = Color.grey;

        public Vector3 vel;
        public Vector3 momentum;

        private Vector3 accumulatedAcceleration;
        private Vector3 accumulatedPosition;    
        public Vector2 atmosphericProperties = Vector2.one;
        public Color atmosphereCol = new Color(0.55f, 0.76f, 0.9f);

        public Vector2 ringBounds;
        /// <summary>
        /// Accelerates sphere by acceleration
        /// </summary>
        /// <param name="acceleration">acceleration</param>
        /// <param name="timestep">timestep, use Renderer.simSpeed * 0.02f</param>
        public void Accelerate(Vector3 acceleration, float timestep)
        {
            accumulatedAcceleration += acceleration * timestep;
            if (accumulatedAcceleration.magnitude > momentum.magnitude * 0.00015f)
            {
                momentum += accumulatedAcceleration;
                accumulatedAcceleration = Vector3.zero;
            }
        }
        /// <summary>
        /// Runs N-body gravity physics
        /// </summary>
        /// <param name="interactables">Spheres to check</param>
        /// <param name="speed">speed of simulation</param>
        public void GravityUpdate(List<SphereObject> interactables, float speed)
        {
            foreach (SphereObject obj in interactables)
            {
                Vector3 normal = (obj.pos - pos).normalized;

                Accelerate(normal * Math.CalculateAcceleration(obj.pos, pos, obj.mass, mass) * 0.04f, speed);
            }
        }
        /// <summary>
        /// Sets the velocity of this sphere to [v].
        /// </summary>
        /// <param name="v">velocity. 24 is the speed of light.</param>
        public void SetVel(Vector3 v)
        {
            float gamma = 1f / Mathf.Sqrt(1f - v.sqrMagnitude * 0.00173611111f);
            momentum = v.normalized * gamma * v.magnitude;
        }
        /// <summary>
        /// Updates the position of this sphere
        /// </summary>
        /// <param name="speed">speed of simulation</param>
        public void UpdatePosition(float speed)
        {
            accumulatedPosition += vel * 0.02f * speed;
            if (accumulatedPosition.magnitude > pos.magnitude * 0.000001f)
            {
                pos += accumulatedPosition;
                accumulatedPosition = Vector3.zero;
            }
        }
    }
}