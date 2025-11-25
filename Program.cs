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
    // --- (Klasa Texture musi być dostępna w projekcie - użyj tej z poprzedniej odpowiedzi) ---

    public class CelestialBody
    {
        public string Name;
        public Texture PlanetTexture;
        public float Size;
        public float OrbitRadius;
        public float OrbitSpeed;
        public float CurrentAngle;

        // Nowe pole: Rodzic (dla Księżyca rodzicem będzie Ziemia, dla Ziemi - null/Słońce)
        public CelestialBody Parent;

        // Metoda obliczająca aktualną pozycję w świecie 3D
        public Vector3 GetPosition()
        {
            // 1. Oblicz pozycję lokalną (względem środka orbity)
            float x = OrbitRadius * (float)Math.Cos(CurrentAngle);
            float z = OrbitRadius * (float)Math.Sin(CurrentAngle);
            Vector3 localPosition = new Vector3(x, 0, z);

            // 2. Jeśli obiekt ma rodzica (jest księżycem), dodaj pozycję rodzica
            if (Parent != null)
            {
                return Parent.GetPosition() + localPosition;
            }

            // Jeśli nie ma rodzica, krąży wokół (0,0,0)
            return localPosition;
        }

        public Matrix4 GetModelMatrix()
        {
            Matrix4 model = Matrix4.CreateScale(Size);

            // Obrót własny
            model *= Matrix4.CreateRotationY(CurrentAngle * 2.0f);

            // Przesunięcie do obliczonej pozycji (uwzględniającej rodzica)
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

        // --- ZMIENNE KAMERY ORBITALNEJ ---
        // Używamy tych zmiennych, bo OnRenderFrame z nich korzysta
        private float _cameraDistance = 30.0f;
        private float _yaw = -90.0f;
        private float _pitch = 30.0f;

        // Zmienne myszki
        private Vector2 _lastMousePos;
        private bool _firstMove = true;

        // Czas
        private float _timeSpeed = 1.0f;

        private List<CelestialBody> _planets = new List<CelestialBody>();

        // --- ZMIENNE DO ŚLEDZENIA (WIDOK Z POWIERZCHNI) ---
        private bool _isLockedToPlanet = false; // Czy kamera jest przyklejona do planety?
        private int _lockedPlanetIndex = 0;     // Indeks śledzonej planety na liście

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
        }

        private void InitializeSolarSystem()
        {
            string path = "Resources/";

            Texture LoadTex(string filename)
            {
                string fullPath = path + filename;
                if (File.Exists(fullPath)) return new Texture(fullPath);
                Console.WriteLine($"[BŁĄD] Brak pliku: {fullPath}");
                return null;
            }

            // --- SŁOŃCE ---
            _planets.Add(new CelestialBody { Name = "Sun", PlanetTexture = LoadTex("2k_sun.jpg"), Size = 4.0f, OrbitRadius = 0, OrbitSpeed = 0 });

            // --- MERKURY ---
            _planets.Add(new CelestialBody { Name = "Mercury", PlanetTexture = LoadTex("2k_mercury.jpg"), Size = 0.4f, OrbitRadius = 6.0f, OrbitSpeed = 1.6f });

            // --- WENUS ---
            _planets.Add(new CelestialBody { Name = "Venus", PlanetTexture = LoadTex("2k_venus_surface.jpg"), Size = 0.9f, OrbitRadius = 9.0f, OrbitSpeed = 1.2f });

            // --- ZIEMIA (Tworzymy zmienną, aby użyć jej przy księżycu) ---
            var earth = new CelestialBody
            {
                Name = "Earth",
                PlanetTexture = LoadTex("2k_earth_daymap.jpg"),
                Size = 1.0f,
                OrbitRadius = 13.0f,
                OrbitSpeed = 1.0f
            };
            _planets.Add(earth);

            // --- KSIĘŻYC ---
            // Ważne: Parent = earth
            _planets.Add(new CelestialBody
            {
                Name = "Moon",
                PlanetTexture = LoadTex("2k_moon.jpg"),
                Size = 0.27f,      // Księżyc jest dużo mniejszy (~1/4 Ziemi)
                OrbitRadius = 2.5f, // Odległość od Ziemi (nie od Słońca!)
                OrbitSpeed = 12.0f, // Krąży znacznie szybciej wokół Ziemi niż Ziemia wokół Słońca
                Parent = earth      // To sprawia, że podąża za Ziemią
            });

            // --- MARS ---
            _planets.Add(new CelestialBody { Name = "Mars", PlanetTexture = LoadTex("2k_mars.jpg"), Size = 0.6f, OrbitRadius = 17.0f, OrbitSpeed = 0.8f });

            // --- JOWISZ ---
            _planets.Add(new CelestialBody { Name = "Jupiter", PlanetTexture = LoadTex("2k_jupiter.jpg"), Size = 2.8f, OrbitRadius = 26.0f, OrbitSpeed = 0.4f });

            // --- SATURN ---
            _planets.Add(new CelestialBody { Name = "Saturn", PlanetTexture = LoadTex("2k_saturn.jpg"), Size = 2.4f, OrbitRadius = 36.0f, OrbitSpeed = 0.3f });

            // --- URAN ---
            _planets.Add(new CelestialBody { Name = "Uranus", PlanetTexture = LoadTex("2k_uranus.jpg"), Size = 1.8f, OrbitRadius = 46.0f, OrbitSpeed = 0.2f });

            // --- NEPTUN ---
            _planets.Add(new CelestialBody { Name = "Neptune", PlanetTexture = LoadTex("2k_neptune.jpg"), Size = 1.7f, OrbitRadius = 56.0f, OrbitSpeed = 0.15f });
        }

        private void InitializeSphereMesh()
        {
            // Generowanie sfery (bez zmian)
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

                    vertices.Add(x); vertices.Add(y); vertices.Add(z);
                    vertices.Add(u); vertices.Add(v);
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

            int stride = 5 * sizeof(float);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);
        }

        // --- KLUCZOWA POPRAWKA: LOGIKA AKTUALIZACJI ---
        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);
            float delta = (float)args.Time;

            var input = KeyboardState;
            var mouse = MouseState;

            if (input.IsKeyDown(Keys.Escape)) Close();

            // --- 1. STEROWANIE CZASEM ---
            if (input.IsKeyDown(Keys.Up)) _timeSpeed += 2.0f * delta;
            if (input.IsKeyDown(Keys.Down)) _timeSpeed -= 2.0f * delta;
            if (_timeSpeed < 0) _timeSpeed = 0;

            // --- 2. PRZEŁĄCZANIE WIDOKU (NOWOŚĆ) ---
            // Używamy IsKeyPressed (działa jednorazowo przy kliknięciu), żeby nie migało
            if (input.IsKeyPressed(Keys.Tab))
            {
                _isLockedToPlanet = !_isLockedToPlanet;

                // Reset ustawień kamery przy zmianie trybu dla wygody
                if (_isLockedToPlanet)
                {
                    // Przybliż do powierzchni przy wejściu w tryb śledzenia
                    _cameraDistance = _planets[_lockedPlanetIndex].Size * 3.0f;
                }
                else
                {
                    // Oddal przy powrocie do widoku ogólnego
                    _cameraDistance = 50.0f;
                }
            }

            // Zmiana planety (Lewo/Prawo) tylko gdy jesteśmy w trybie śledzenia
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

            // --- 3. STEROWANIE KAMERĄ (MYSZKA) ---
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

            // --- 4. STEROWANIE ZOOMEM (SCROLL) ---
            float scroll = mouse.ScrollDelta.Y;
            _cameraDistance -= scroll * 2.0f;

            // Zabezpieczenie zooma:
            // Jeśli śledzimy planetę, nie pozwalamy wejść "pod ziemię" (minimalny dystans to rozmiar planety)
            float minZoom = 2.0f;
            if (_isLockedToPlanet)
            {
                minZoom = _planets[_lockedPlanetIndex].Size * 1.2f; // 1.2x promienia, żeby nie przenikać tekstury
            }

            if (_cameraDistance < minZoom) _cameraDistance = minZoom;
            if (_cameraDistance > 300.0f) _cameraDistance = 300.0f;

            // --- 5. AKTUALIZACJA FIZYKI PLANET ---
            foreach (var planet in _planets)
            {
                planet.Update(delta, _timeSpeed);
            }

            // Aktualizacja tytułu okna
            string modeInfo = _isLockedToPlanet ? $"Śledzenie: {_planets[_lockedPlanetIndex].Name}" : "Widok swobodny";
            Title = $"Planetarium | {modeInfo} | Czas: x{_timeSpeed:F1} | [TAB] Zmień widok | [Strzałki] Czas/Planeta";
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.UseProgram(_shaderProgram);
            GL.BindVertexArray(_vao);

            // --- OBLICZANIE POZYCJI KAMERY ---

            // 1. Ustal punkt skupienia (Focus Point)
            Vector3 targetPosition = Vector3.Zero; // Domyślnie Słońce (0,0,0)

            if (_isLockedToPlanet)
            {
                // Jeśli śledzimy, punktem skupienia jest aktualna pozycja planety
                targetPosition = _planets[_lockedPlanetIndex].GetPosition();
            }

            // 2. Oblicz pozycję kamery względem punktu skupienia (sferycznie)
            float camX = _cameraDistance * (float)Math.Cos(MathHelper.DegreesToRadians(_pitch)) * (float)Math.Cos(MathHelper.DegreesToRadians(_yaw));
            float camY = _cameraDistance * (float)Math.Sin(MathHelper.DegreesToRadians(_pitch));
            float camZ = _cameraDistance * (float)Math.Cos(MathHelper.DegreesToRadians(_pitch)) * (float)Math.Sin(MathHelper.DegreesToRadians(_yaw));

            // 3. Pozycja finalna to: Pozycja Planety + Offset Kamery
            Vector3 cameraPosition = targetPosition + new Vector3(camX, camY, camZ);

            // 4. Matrix View: Kamera patrzy NA targetPosition
            Matrix4 view = Matrix4.LookAt(cameraPosition, targetPosition, Vector3.UnitY);

            Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45.0f), Size.X / (float)Size.Y, 0.1f, 1000.0f);

            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "view"), false, ref view);
            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "projection"), false, ref projection);

            int modelLoc = GL.GetUniformLocation(_shaderProgram, "model");

            foreach (var planet in _planets)
            {
                if (planet.PlanetTexture != null) planet.PlanetTexture.Use(TextureUnit.Texture0);

                Matrix4 model = planet.GetModelMatrix();
                GL.UniformMatrix4(modelLoc, false, ref model);
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
            string vertSource = @"
                #version 330 core
                layout (location = 0) in vec3 aPosition;
                layout (location = 1) in vec2 aTexCoord;
                out vec2 TexCoord;
                uniform mat4 model;
                uniform mat4 view;
                uniform mat4 projection;
                void main()
                {
                    gl_Position = projection * view * model * vec4(aPosition, 1.0);
                    TexCoord = aTexCoord;
                }";

            string fragSource = @"
                #version 330 core
                out vec4 FragColor;
                in vec2 TexCoord;
                uniform sampler2D texture0;
                void main()
                {
                    FragColor = texture(texture0, TexCoord);
                }";

            int vert = CompileShader(ShaderType.VertexShader, vertSource);
            int frag = CompileShader(ShaderType.FragmentShader, fragSource);

            _shaderProgram = GL.CreateProgram();
            GL.AttachShader(_shaderProgram, vert);
            GL.AttachShader(_shaderProgram, frag);
            GL.LinkProgram(_shaderProgram);

            GL.DeleteShader(vert);
            GL.DeleteShader(frag);
        }

        private int CompileShader(ShaderType type, string source)
        {
            int shader = GL.CreateShader(type);
            GL.ShaderSource(shader, source);
            GL.CompileShader(shader);
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