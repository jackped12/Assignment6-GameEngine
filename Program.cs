using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.IO;
using Windows_Engine;
namespace Windows_Engine
{
    public class Game : GameWindow
    {
        private int shaderProgram;
        private int vaoCube, vaoPyramid, vaoGround;
        private int texGround, texCube;
        private Camera camera = new Camera();
        private Interact cubeInteract;
        private Interact pyramidInteract;
        private Interact PickedUpObject;
        private bool firstMouse = true;
        private Vector2 lastMousePos;
        private Matrix4 projection;
        private float fov = 60f;
        private int uModel, uView, uProj, uViewPos, uLightPos, uLightColor, uLightIntensity;
        private int uMatAmbient, uMatDiffuse, uMatSpecular, uMatShininess;
        private int uTexture;
        // Embedded shader sources (to avoid file dependencies)
        private static readonly string VertexShaderSource = @"
            #version 330 core
            layout (location = 0) in vec3 aPos;
            layout (location = 1) in vec3 aNormal;
            layout (location = 2) in vec2 aTexCoord;
            out vec3 FragPos;
            out vec3 Normal;
            out vec2 TexCoord;
            uniform mat4 model;
            uniform mat4 view;
            uniform mat4 projection;
            void main()
            {
                FragPos = vec3(model * vec4(aPos, 1.0));
                Normal = mat3(transpose(inverse(model))) * aNormal;
                TexCoord = vec2(aTexCoord.x, aTexCoord.y);
                gl_Position = projection * view * vec4(FragPos, 1.0);
            }
        ";
        private static readonly string FragmentShaderSource = @"
            #version 330 core
            out vec4 FragColor;
            in vec3 FragPos;
            in vec3 Normal;
            in vec2 TexCoord;
            uniform vec3 lightPos;
            uniform vec3 viewPos;
            uniform vec3 lightColor;
            uniform float lightIntensity;
            uniform vec3 matAmbient;
            uniform vec3 matDiffuse;
            uniform vec3 matSpecular;
            uniform float matShininess;
            uniform sampler2D uTexture;
            void main()
            {
                vec3 texColor = texture(uTexture, TexCoord).rgb;
                vec3 ambient = matAmbient * lightColor * lightIntensity;
                vec3 norm = normalize(Normal);
                vec3 lightDir = normalize(lightPos - FragPos);
                float diff = max(dot(norm, lightDir), 0.0);
                vec3 diffuse = diff * matDiffuse * lightColor * lightIntensity;
                vec3 viewDir = normalize(viewPos - FragPos);
                vec3 reflectDir = reflect(-lightDir, norm);
                float spec = pow(max(dot(viewDir, reflectDir), 0.0), matShininess);
                vec3 specular = spec * matSpecular * lightColor * lightIntensity;
                vec3 result = (ambient + diffuse + specular) * texColor;
                FragColor = vec4(result, 1.0);
            }
        ";
        public Game(GameWindowSettings gws, NativeWindowSettings nws) : base(gws, nws)
        {
            CursorState = CursorState.Grabbed;
            cubeInteract = new Interact(Vector3.Zero);
            pyramidInteract = new Interact(new Vector3(2f, 0.5f, -2f));
            PickedUpObject = null;
        }
        protected override void OnLoad()
        {
            base.OnLoad();
            GL.ClearColor(0.1f, 0.12f, 0.15f, 1f);
            GL.Enable(EnableCap.DepthTest);
            // Compile shaders from embedded sources
            int v = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(v, VertexShaderSource);
            GL.CompileShader(v);
            CheckShaderCompile(v);
            int f = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(f, FragmentShaderSource);
            GL.CompileShader(f);
            CheckShaderCompile(f);
            shaderProgram = GL.CreateProgram();
            GL.AttachShader(shaderProgram, v);
            GL.AttachShader(shaderProgram, f);
            GL.LinkProgram(shaderProgram);
            CheckProgramLink(shaderProgram);
            GL.DeleteShader(v);
            GL.DeleteShader(f);
            uModel = GL.GetUniformLocation(shaderProgram, "model");
            uView = GL.GetUniformLocation(shaderProgram, "view");
            uProj = GL.GetUniformLocation(shaderProgram, "projection");
            uViewPos = GL.GetUniformLocation(shaderProgram, "viewPos");
            uLightPos = GL.GetUniformLocation(shaderProgram, "lightPos");
            uLightColor = GL.GetUniformLocation(shaderProgram, "lightColor");
            uLightIntensity = GL.GetUniformLocation(shaderProgram, "lightIntensity");
            uMatAmbient = GL.GetUniformLocation(shaderProgram, "matAmbient");
            uMatDiffuse = GL.GetUniformLocation(shaderProgram, "matDiffuse");
            uMatSpecular = GL.GetUniformLocation(shaderProgram, "matSpecular");
            uMatShininess = GL.GetUniformLocation(shaderProgram, "matShininess");
            uTexture = GL.GetUniformLocation(shaderProgram, "uTexture");
            vaoCube = CreateMesh(ShapeFactory.CreateCubeTextured());
            vaoPyramid = CreateMesh(ShapeFactory.CreatePyramidTextured());
            vaoGround = CreateMesh(ShapeFactory.CreatePlaneTextured(10f));
            texCube = TextureLoader.LoadTexture(); // Dummy textures
            texGround = TextureLoader.LoadTexture();
            float aspectRatio = (float)Size.X / Size.Y;
            projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(fov), aspectRatio, 0.1f, 100f);
        }
        private int CreateMesh(float[] vertices)
        {
            int vao = GL.GenVertexArray();
            int vbo = GL.GenBuffer();
            GL.BindVertexArray(vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
            int stride = 8 * sizeof(float);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));
            GL.EnableVertexAttribArray(2);
            GL.BindVertexArray(0);
            return vao;
        }
        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);
            var kb = KeyboardState;
            var ms = MouseState;
            if (kb.IsKeyDown(Keys.Escape))
                Close();
            // Frame-rate independent movement
            float cameraSpeed = 5f * (float)args.Time;
            Vector3 right = Vector3.Normalize(Vector3.Cross(camera.Front, camera.Up));
            if (kb.IsKeyDown(Keys.W))
                camera.Position += camera.Front * cameraSpeed;
            if (kb.IsKeyDown(Keys.S))
                camera.Position -= camera.Front * cameraSpeed;
            if (kb.IsKeyDown(Keys.A))
                camera.Position -= right * cameraSpeed;
            if (kb.IsKeyDown(Keys.D))
                camera.Position += right * cameraSpeed;
            // Update picked up object position
            if (PickedUpObject != null)
            {
                PickedUpObject.ItemPosition = camera.Position + camera.Front * 1.5f + new Vector3(0f, 0.5f, 0f);
            }
            // FPS-style mouse look: always active when cursor grabbed (no right-click required)
            if (firstMouse)
            {
                lastMousePos = ms.Position;
                firstMouse = false;
            }
            else
            {
                float xoffset = ms.Position.X - lastMousePos.X;
                float yoffset = lastMousePos.Y - ms.Position.Y; // Reversed Y for natural look
                lastMousePos = ms.Position;
                camera.UpdateDirection(xoffset, yoffset);
            }
            // Mouse wheel zoom (adjust FOV)
            float scrollDelta = ms.Scroll.Y;
            if (scrollDelta != 0f)
            {
                fov -= scrollDelta * 2.5f;
                fov = MathHelper.Clamp(fov, 10f, 90f);
                float aspectRatio = (float)Size.X / Size.Y;
                projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(fov), aspectRatio, 0.1f, 100f);
            }
            if (kb.IsKeyPressed(Keys.E))
            {
                if (PickedUpObject != null)
                {
                    // Drop
                    PickedUpObject.ItemPosition = camera.Position + camera.Front * 2f;
                    PickedUpObject = null;
                    Console.WriteLine("Dropped!");
                }
                else
                {
                    // Try to pick up
                    float distCube = Vector3.Distance(camera.Position, cubeInteract.ItemPosition);
                    float distPyramid = Vector3.Distance(camera.Position, pyramidInteract.ItemPosition);
                    if (distCube < 2.0f || distPyramid < 2.0f)
                    {
                        if (distCube <= distPyramid)
                        {
                            PickedUpObject = cubeInteract;
                            Console.WriteLine("Picked up cube!");
                        }
                        else
                        {
                            PickedUpObject = pyramidInteract;
                            Console.WriteLine("Picked up pyramid!");
                        }
                    }
                }
            }
        }
        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.UseProgram(shaderProgram);
            GL.Uniform3(uViewPos, camera.Position);
            GL.Uniform3(uLightPos, new Vector3(0f, 4f, 4f));
            GL.Uniform3(uLightColor, new Vector3(1f, 1f, 1f));
            GL.Uniform1(uLightIntensity, 2.0f);
            GL.Uniform3(uMatAmbient, new Vector3(0.2f));
            GL.Uniform3(uMatDiffuse, new Vector3(0.8f, 0.6f, 0.4f));
            GL.Uniform3(uMatSpecular, new Vector3(0.8f));
            GL.Uniform1(uMatShininess, 64f);
            Matrix4 view = camera.GetViewMatrix();
            GL.UniformMatrix4(uView, false, ref view);
            GL.UniformMatrix4(uProj, false, ref projection);
            // Draw ground
            GL.BindVertexArray(vaoGround);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, texGround);
            GL.Uniform1(uTexture, 0);
            Matrix4 model = Matrix4.CreateTranslation(0f, -1f, 0f);
            GL.UniformMatrix4(uModel, false, ref model);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
            // Draw cube
            GL.BindVertexArray(vaoCube);
            GL.BindTexture(TextureTarget.Texture2D, texCube);
            GL.Uniform1(uTexture, 0);
            model = Matrix4.CreateTranslation(cubeInteract.ItemPosition);
            GL.UniformMatrix4(uModel, false, ref model);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 36);
            // Draw pyramid
            GL.BindVertexArray(vaoPyramid);
            GL.BindTexture(TextureTarget.Texture2D, texCube); // reuse cube tex
            GL.Uniform1(uTexture, 0);
            model = Matrix4.CreateTranslation(pyramidInteract.ItemPosition);
            GL.UniformMatrix4(uModel, false, ref model);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 18);
            SwapBuffers();
        }
        private void CheckShaderCompile(int shader)
        {
            GL.GetShader(shader, ShaderParameter.CompileStatus, out var status);
            if (status == (int)All.False)
                throw new Exception(GL.GetShaderInfoLog(shader));
        }
        private void CheckProgramLink(int program)
        {
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out var status);
            if (status == (int)All.False)
                throw new Exception(GL.GetProgramInfoLog(program));
        }
        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, Size.X, Size.Y);
            float aspectRatio = (float)Size.X / Size.Y;
            projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(fov), aspectRatio, 0.1f, 100f);
        }
        protected override void OnUnload()
        {
            GL.DeleteProgram(shaderProgram);
            GL.DeleteVertexArray(vaoCube);
            GL.DeleteVertexArray(vaoPyramid);
            GL.DeleteVertexArray(vaoGround);
            GL.DeleteTexture(texCube);
            GL.DeleteTexture(texGround);
            base.OnUnload();
        }
    }
    // Added: Camera class for FPS-style movement and look
    public class Camera
    {
        public Vector3 Position { get; set; } = new Vector3(0.0f, 1.5f, 3.0f);
        private Vector3 _front = new Vector3(0.0f, 0.0f, -1.0f);
        public Vector3 Front => _front;
        public Vector3 Up { get; } = Vector3.UnitY;
        public Vector3 Right => Vector3.Normalize(Vector3.Cross(_front, Up));
        public float Yaw { get; private set; } = -MathHelper.PiOver2;
        public float Pitch { get; private set; }
        public float MovementSpeed { get; set; } = 2.5f;
        public float MouseSensitivity { get; set; } = 0.05f; // Reduced for less sensitivity
        public Matrix4 GetViewMatrix()
        {
            return Matrix4.LookAt(Position, Position + _front, Up);
        }
        public void UpdateDirection(float xoffset, float yoffset)
        {
            Yaw -= xoffset * MouseSensitivity; // Reversed left-right (negated xoffset)
            Pitch += yoffset * MouseSensitivity;
            // Clamp pitch to prevent flipping
            Pitch = MathHelper.Clamp(Pitch, -MathHelper.PiOver2 + 0.01f, MathHelper.PiOver2 - 0.01f);
            // Update front vector
            _front.X = (float)(Math.Cos((double)Pitch) * Math.Sin((double)Yaw));
            _front.Y = (float)Math.Sin((double)Pitch);
            _front.Z = (float)(Math.Cos((double)Pitch) * Math.Cos((double)Yaw));
            _front = Vector3.Normalize(_front);
        }
    }
    // Added: Simple Interact class
    public class Interact
    {
        public Vector3 ItemPosition { get; set; }
        public Interact(Vector3 pos)
        {
            ItemPosition = pos;
        }
    }
    // Added: Simple TextureLoader (dummy 1x1 textures for demo; replace with real loading if assets available)
    public static class TextureLoader
    {
        public static int LoadTexture()
        {
            int tex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, tex);
            // Dummy 1x1 white texture
            byte[] data = { 255, 255, 255, 255 }; // RGBA white
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, 1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, data);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            return tex;
        }
    }
    public static class ShapeFactory
    {
        // Position(x,y,z), Normal(x,y,z), TexCoord(u,v)
        public static float[] CreateCubeTextured()
        {
            float s = 0.5f;
            return new float[]
            {
                // back face
                -s,-s,-s, 0,0,-1, 0,0,
                 s,-s,-s, 0,0,-1, 1,0,
                 s, s,-s, 0,0,-1, 1,1,
                 s, s,-s, 0,0,-1, 1,1,
                -s, s,-s, 0,0,-1, 0,1,
                -s,-s,-s, 0,0,-1, 0,0,
                // front face
                -s,-s, s, 0,0,1, 0,0,
                 s,-s, s, 0,0,1, 1,0,
                 s, s, s, 0,0,1, 1,1,
                 s, s, s, 0,0,1, 1,1,
                -s, s, s, 0,0,1, 0,1,
                -s,-s, s, 0,0,1, 0,0,
                // left face
                -s, s, s, -1,0,0, 1,0,
                -s, s,-s, -1,0,0, 1,1,
                -s, -s,-s, -1,0,0, 0,1,
                -s, -s,-s, -1,0,0, 0,1,
                -s, -s, s, -1,0,0, 0,0,
                -s, s, s, -1,0,0, 1,0,
                // right face
                 s, s, s, 1,0,0, 1,0,
                 s, s,-s, 1,0,0, 1,1,
                 s, -s,-s, 1,0,0, 0,1,
                 s, -s,-s, 1,0,0, 0,1,
                 s, -s, s, 1,0,0, 0,0,
                 s, s, s, 1,0,0, 1,0,
                // bottom face
                -s, -s,-s, 0,-1,0, 0,1,
                 s, -s,-s, 0,-1,0, 1,1,
                 s, -s, s, 0,-1,0, 1,0,
                 s, -s, s, 0,-1,0, 1,0,
                -s, -s, s, 0,-1,0, 0,0,
                -s, -s,-s, 0,-1,0, 0,1,
                // top face
                -s, s,-s, 0,1,0, 0,1,
                 s, s,-s, 0,1,0, 1,1,
                 s, s, s, 0,1,0, 1,0,
                 s, s, s, 0,1,0, 1,0,
                -s, s, s, 0,1,0, 0,0,
                -s, s,-s, 0,1,0, 0,1,
            };
        }
        public static float[] CreatePyramidTextured()
        {
            return new float[]
            {
                // Base (two triangles)
                -0.5f, 0f, -0.5f, 0, -1, 0, 0, 0,
                 0.5f, 0f, -0.5f, 0, -1, 0, 1, 0,
                 0.5f, 0f, 0.5f, 0, -1, 0, 1, 1,
                 0.5f, 0f, 0.5f, 0, -1, 0, 1, 1,
                -0.5f, 0f, 0.5f, 0, -1, 0, 0, 1,
                -0.5f, 0f, -0.5f, 0, -1, 0, 0, 0,
                // Sides
                -0.5f, 0f, -0.5f, 0, 0.707f, -0.707f, 0, 0,
                 0.5f, 0f, -0.5f, 0, 0.707f, -0.707f, 1, 0,
                 0f, 0.8f, 0f, 0, 0.707f, -0.707f, 0.5f, 1,
                 0.5f, 0f, -0.5f, 0.707f, 0, -0.707f, 0, 0,
                 0.5f, 0f, 0.5f, 0.707f, 0, -0.707f, 1, 0,
                 0f, 0.8f, 0f, 0.707f, 0, -0.707f, 0.5f, 1,
                 0.5f, 0f, 0.5f, 0, 0.707f, 0.707f, 0, 0,
                -0.5f, 0f, 0.5f, 0, 0.707f, 0.707f, 1, 0,
                 0f, 0.8f, 0f, 0, 0.707f, 0.707f, 0.5f, 1,
                -0.5f, 0f, 0.5f, -0.707f, 0, 0.707f, 0, 0,
                -0.5f, 0f, -0.5f, -0.707f, 0, 0.707f, 1, 0,
                 0f, 0.8f, 0f, -0.707f, 0, 0.707f, 0.5f, 1,
            };
        }
        public static float[] CreatePlaneTextured(float size)
        {
            float s = size / 2f;
            return new float[]
            {
                -s, 0, -s, 0, 1, 0, 0, 0,
                 s, 0, -s, 0, 1, 0, 1, 0,
                 s, 0, s, 0, 1, 0, 1, 1,
                 s, 0, s, 0, 1, 0, 1, 1,
                -s, 0, s, 0, 1, 0, 0, 1,
                -s, 0, -s, 0, 1, 0, 0, 0,
            };
        }
    }
}
class Program
{
    static void Main()
    {
        var nativeWindowSettings = new NativeWindowSettings()
        {
            ClientSize = new Vector2i(800, 600),
            Title = "OpenTK FPS Camera Demo"
        };
        using (var window = new Game(GameWindowSettings.Default, nativeWindowSettings))
        {
            window.Run();
        }
    }
}