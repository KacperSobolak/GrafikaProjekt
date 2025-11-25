using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Collections.Generic;
using System.IO;
using System;

namespace Planetarium3D
{
    public class CelestialBody
    {
        public string Name;
        public Texture PlanetTexture;
        public float Size;
        public float OrbitRadius;
        public float OrbitSpeed;
        public float CurrentAngle;
        public bool IsSun = false;

        public CelestialBody Parent;

        public Vector3 GetPosition()
        {
            float x = OrbitRadius * (float)Math.Cos(CurrentAngle);
            float z = OrbitRadius * (float)Math.Sin(CurrentAngle);
            Vector3 localPosition = new Vector3(x, 0, z);

            if (Parent != null)
            {
                return Parent.GetPosition() + localPosition;
            }

            return localPosition;
        }

        public Matrix4 GetModelMatrix()
        {
            Matrix4 model = Matrix4.CreateScale(Size);
            model *= Matrix4.CreateRotationY(CurrentAngle * 2.0f);
            Vector3 finalPosition = GetPosition();
            model *= Matrix4.CreateTranslation(finalPosition);
            return model;
        }

        public void Update(float deltaTime, float timeSpeed)
        {
            CurrentAngle += OrbitSpeed * deltaTime * timeSpeed;
            if (CurrentAngle > MathHelper.TwoPi) CurrentAngle -= MathHelper.TwoPi;
        }
    }

    public class PlanetariumWindow : GameWindow
    {
        private int _vao;
        private int _vbo;
        private int _ebo;
        private int _vertexCount;
        private int _shaderProgram;

        private float _cameraDistance = 30.0f;
        private float _yaw = -90.0f;
        private float _pitch = 30.0f;

        private Vector2 _lastMousePos;
        private bool _firstMove = true;

        private float _timeSpeed = 1.0f;

        private List<CelestialBody> _planets = new List<CelestialBody>();

        private bool _isLockedToPlanet = false;
        private int _lockedPlanetIndex = 0;

        public PlanetariumWindow(GameWindowSettings gameSettings, NativeWindowSettings nativeSettings)
            : base(gameSettings, nativeSettings) { }

        protected override void OnLoad()
        {
            base.OnLoad();
            GL.ClearColor(0.02f, 0.02f, 0.05f, 1.0f);
            GL.Enable(EnableCap.DepthTest);

            InitializeShaders();
            InitializeSphereMesh();
            InitializeSolarSystem();

            Console.WriteLine("OpenGL version: " + GL.GetString(StringName.Version));
            Console.WriteLine("Shader compiled successfully");
        }

        private void InitializeSolarSystem()
        {
            string path = "Resources/";

            Texture LoadTex(string filename)
            {
                string fullPath = path + filename;
                if (File.Exists(fullPath))
                {
                    Console.WriteLine($"Loading texture: {fullPath}");
                    return new Texture(fullPath);
                }
                Console.WriteLine($"[BŁĄD] Brak pliku: {fullPath}");
                return new Texture(""); // Fallback texture
            }

            // SŁOŃCE
            _planets.Add(new CelestialBody
            {
                Name = "Sun",
                PlanetTexture = LoadTex("2k_sun.jpg"),
                Size = 4.0f,
                OrbitRadius = 0,
                OrbitSpeed = 0,
                IsSun = true
            });

            // MERKURY
            _planets.Add(new CelestialBody { Name = "Mercury", PlanetTexture = LoadTex("2k_mercury.jpg"), Size = 0.4f, OrbitRadius = 6.0f, OrbitSpeed = 1.6f });

            // WENUS
            _planets.Add(new CelestialBody { Name = "Venus", PlanetTexture = LoadTex("2k_venus_surface.jpg"), Size = 0.9f, OrbitRadius = 9.0f, OrbitSpeed = 1.2f });

            // ZIEMIA
            var earth = new CelestialBody
            {
                Name = "Earth",
                PlanetTexture = LoadTex("2k_earth_daymap.jpg"),
                Size = 1.0f,
                OrbitRadius = 13.0f,
                OrbitSpeed = 1.0f
            };
            _planets.Add(earth);

            // KSIĘŻYC
            _planets.Add(new CelestialBody
            {
                Name = "Moon",
                PlanetTexture = LoadTex("2k_moon.jpg"),
                Size = 0.27f,
                OrbitRadius = 2.5f,
                OrbitSpeed = 12.0f,
                Parent = earth
            });

            // MARS
            _planets.Add(new CelestialBody { Name = "Mars", PlanetTexture = LoadTex("2k_mars.jpg"), Size = 0.6f, OrbitRadius = 17.0f, OrbitSpeed = 0.8f });

            // JOWISZ
            _planets.Add(new CelestialBody { Name = "Jupiter", PlanetTexture = LoadTex("2k_jupiter.jpg"), Size = 2.8f, OrbitRadius = 26.0f, OrbitSpeed = 0.4f });

            // SATURN
            _planets.Add(new CelestialBody { Name = "Saturn", PlanetTexture = LoadTex("2k_saturn.jpg"), Size = 2.4f, OrbitRadius = 36.0f, OrbitSpeed = 0.3f });

            // URAN
            _planets.Add(new CelestialBody { Name = "Uranus", PlanetTexture = LoadTex("2k_uranus.jpg"), Size = 1.8f, OrbitRadius = 46.0f, OrbitSpeed = 0.2f });

            // NEPTUN
            _planets.Add(new CelestialBody { Name = "Neptune", PlanetTexture = LoadTex("2k_neptune.jpg"), Size = 1.7f, OrbitRadius = 56.0f, OrbitSpeed = 0.15f });
        }

        private void InitializeSphereMesh()
        {
            List<float> vertices = new List<float>();
            List<uint> indices = new List<uint>();

            int latitudeBands = 30;
            int longitudeBands = 30;

            for (int lat = 0; lat <= latitudeBands; lat++)
            {
                float theta = lat * MathHelper.Pi / latitudeBands;
                float sinTheta = (float)Math.Sin(theta);
                float cosTheta = (float)Math.Cos(theta);

                for (int lon = 0; lon <= longitudeBands; lon++)
                {
                    float phi = lon * 2 * MathHelper.Pi / longitudeBands;
                    float sinPhi = (float)Math.Sin(phi);
                    float cosPhi = (float)Math.Cos(phi);

                    float x = cosPhi * sinTheta;
                    float y = cosTheta;
                    float z = sinPhi * sinTheta;
                    float u = (float)lon / longitudeBands;
                    float v = (float)lat / latitudeBands;

                    // Position
                    vertices.Add(x); vertices.Add(y); vertices.Add(z);
                    // Texture coordinates
                    vertices.Add(u); vertices.Add(v);
                    // Normal (same as position for unit sphere)
                    vertices.Add(x); vertices.Add(y); vertices.Add(z);
                }
            }

            for (int lat = 0; lat < latitudeBands; lat++)
            {
                for (int lon = 0; lon < longitudeBands; lon++)
                {
                    int first = (lat * (longitudeBands + 1)) + lon;
                    int second = first + longitudeBands + 1;
                    indices.Add((uint)first); indices.Add((uint)second); indices.Add((uint)(first + 1));
                    indices.Add((uint)second); indices.Add((uint)(second + 1)); indices.Add((uint)(first + 1));
                }
            }

            _vertexCount = indices.Count;
            float[] finalVertices = vertices.ToArray();
            uint[] finalIndices = indices.ToArray();

            _vao = GL.GenVertexArray();
            GL.BindVertexArray(_vao);

            _vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, finalVertices.Length * sizeof(float), finalVertices, BufferUsageHint.StaticDraw);

            _ebo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, finalIndices.Length * sizeof(uint), finalIndices, BufferUsageHint.StaticDraw);

            // Position attribute
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            // Texture coordinate attribute
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            // Normal attribute
            GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 5 * sizeof(float));
            GL.EnableVertexAttribArray(2);

            Console.WriteLine($"Mesh created: {_vertexCount} vertices");
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);
            float delta = (float)args.Time;

            var input = KeyboardState;
            var mouse = MouseState;

            if (input.IsKeyDown(Keys.Escape)) Close();

            // Time control
            if (input.IsKeyDown(Keys.Up)) _timeSpeed += 2.0f * delta;
            if (input.IsKeyDown(Keys.Down)) _timeSpeed -= 2.0f * delta;
            if (_timeSpeed < 0) _timeSpeed = 0;

            // View switching
            if (input.IsKeyPressed(Keys.Tab))
            {
                _isLockedToPlanet = !_isLockedToPlanet;
                if (_isLockedToPlanet)
                {
                    _cameraDistance = _planets[_lockedPlanetIndex].Size * 3.0f;
                }
                else
                {
                    _cameraDistance = 50.0f;
                }
            }

            // Planet switching
            if (_isLockedToPlanet)
            {
                if (input.IsKeyPressed(Keys.Right))
                {
                    _lockedPlanetIndex++;
                    if (_lockedPlanetIndex >= _planets.Count) _lockedPlanetIndex = 0;
                }
                if (input.IsKeyPressed(Keys.Left))
                {
                    _lockedPlanetIndex--;
                    if (_lockedPlanetIndex < 0) _lockedPlanetIndex = _planets.Count - 1;
                }
            }

            // Camera control
            if (_firstMove)
            {
                _lastMousePos = new Vector2(mouse.X, mouse.Y);
                _firstMove = false;
            }
            else
            {
                if (mouse.IsButtonDown(MouseButton.Left))
                {
                    float deltaX = mouse.X - _lastMousePos.X;
                    float deltaY = mouse.Y - _lastMousePos.Y;
                    float sensitivity = 0.3f;

                    _yaw += deltaX * sensitivity;
                    _pitch -= deltaY * sensitivity;

                    if (_pitch > 89.0f) _pitch = 89.0f;
                    if (_pitch < -89.0f) _pitch = -89.0f;
                }
                _lastMousePos = new Vector2(mouse.X, mouse.Y);
            }

            // Zoom
            float scroll = mouse.ScrollDelta.Y;
            _cameraDistance -= scroll * 2.0f;

            float minZoom = 2.0f;
            if (_isLockedToPlanet)
            {
                minZoom = _planets[_lockedPlanetIndex].Size * 1.2f;
            }

            if (_cameraDistance < minZoom) _cameraDistance = minZoom;
            if (_cameraDistance > 300.0f) _cameraDistance = 300.0f;

            // Update planets
            foreach (var planet in _planets)
            {
                planet.Update(delta, _timeSpeed);
            }

            // Update title
            string modeInfo = _isLockedToPlanet ? $"Śledzenie: {_planets[_lockedPlanetIndex].Name}" : "Widok swobodny";
            Title = $"Planetarium | {modeInfo} | Czas: x{_timeSpeed:F1} | [TAB] Zmień widok | [Strzałki] Czas/Planeta";
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.UseProgram(_shaderProgram);
            GL.BindVertexArray(_vao);

            // Calculate camera
            Vector3 targetPosition = Vector3.Zero;
            if (_isLockedToPlanet)
            {
                targetPosition = _planets[_lockedPlanetIndex].GetPosition();
            }

            float camX = _cameraDistance * (float)Math.Cos(MathHelper.DegreesToRadians(_pitch)) * (float)Math.Cos(MathHelper.DegreesToRadians(_yaw));
            float camY = _cameraDistance * (float)Math.Sin(MathHelper.DegreesToRadians(_pitch));
            float camZ = _cameraDistance * (float)Math.Cos(MathHelper.DegreesToRadians(_pitch)) * (float)Math.Sin(MathHelper.DegreesToRadians(_yaw));

            Vector3 cameraPosition = targetPosition + new Vector3(camX, camY, camZ);
            Matrix4 view = Matrix4.LookAt(cameraPosition, targetPosition, Vector3.UnitY);
            Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45.0f), Size.X / (float)Size.Y, 0.1f, 1000.0f);

            // Set matrices
            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "view"), false, ref view);
            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "projection"), false, ref projection);

            // Set sun position
            Vector3 sunPosition = _planets[0].GetPosition();
            GL.Uniform3(GL.GetUniformLocation(_shaderProgram, "sunPosition"), sunPosition);

            int modelLoc = GL.GetUniformLocation(_shaderProgram, "model");
            int isSunLoc = GL.GetUniformLocation(_shaderProgram, "isSun");

            // Render planets
            foreach (var planet in _planets)
            {
                if (planet.PlanetTexture != null)
                    planet.PlanetTexture.Use(TextureUnit.Texture0);

                Matrix4 model = planet.GetModelMatrix();
                GL.UniformMatrix4(modelLoc, false, ref model);

                // Use int instead of bool for compatibility
                GL.Uniform1(isSunLoc, planet.IsSun ? 1 : 0);

                GL.DrawElements(PrimitiveType.Triangles, _vertexCount, DrawElementsType.UnsignedInt, 0);
            }

            SwapBuffers();
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, e.Width, e.Height);
        }

        private void InitializeShaders()
        {
            // Simple vertex shader
            string vertSource = @"
                #version 330 core
                layout (location = 0) in vec3 aPosition;
                layout (location = 1) in vec2 aTexCoord;
                layout (location = 2) in vec3 aNormal;
                
                out vec2 TexCoord;
                out vec3 Normal;
                out vec3 FragPos;
                
                uniform mat4 model;
                uniform mat4 view;
                uniform mat4 projection;
                
                void main()
                {
                    gl_Position = projection * view * model * vec4(aPosition, 1.0);
                    TexCoord = aTexCoord;
                    Normal = mat3(transpose(inverse(model))) * aNormal;
                    FragPos = vec3(model * vec4(aPosition, 1.0));
                }";

            // Fragment shader with lighting
            string fragSource = @"
                #version 330 core
                out vec4 FragColor;
                
                in vec2 TexCoord;
                in vec3 Normal;
                in vec3 FragPos;
                
                uniform sampler2D texture0;
                uniform vec3 sunPosition;
                uniform int isSun;
                
                void main()
                {
                    vec4 textureColor = texture(texture0, TexCoord);
                    
                    // If it's the sun, display without lighting
                    if (isSun == 1) {
                        FragColor = textureColor;
                        return;
                    }
                    
                    // Calculate lighting for planets
                    vec3 lightDir = normalize(sunPosition - FragPos);
                    vec3 norm = normalize(Normal);
                    float diff = max(dot(norm, lightDir), 0.0);
                    
                    // Simple lighting: day side bright, night side dark
                    float ambient = 0.2;
                    float lighting = ambient + (1.0 - ambient) * diff;
                    
                    FragColor = textureColor * lighting;
                }";

            int vert = CompileShader(ShaderType.VertexShader, vertSource);
            int frag = CompileShader(ShaderType.FragmentShader, fragSource);

            _shaderProgram = GL.CreateProgram();
            GL.AttachShader(_shaderProgram, vert);
            GL.AttachShader(_shaderProgram, frag);
            GL.LinkProgram(_shaderProgram);

            // Check for linking errors
            GL.GetProgram(_shaderProgram, GetProgramParameterName.LinkStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetProgramInfoLog(_shaderProgram);
                Console.WriteLine($"Shader linking error: {infoLog}");
            }

            GL.DeleteShader(vert);
            GL.DeleteShader(frag);

            Console.WriteLine("Shaders initialized successfully");
        }

        private int CompileShader(ShaderType type, string source)
        {
            int shader = GL.CreateShader(type);
            GL.ShaderSource(shader, source);
            GL.CompileShader(shader);

            GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetShaderInfoLog(shader);
                Console.WriteLine($"Shader compilation error ({type}): {infoLog}");
            }

            return shader;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var nativeSettings = new NativeWindowSettings()
            {
                Size = new Vector2i(1024, 768),
                Title = "OpenTK Planetarium",
                APIVersion = new Version(3, 3)
            };

            using (var game = new PlanetariumWindow(GameWindowSettings.Default, nativeSettings))
            {
                game.Run();
            }
        }
    }
}