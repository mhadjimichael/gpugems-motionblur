using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MotionBlur
{
    public class Game1 : Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        SpriteFont font;
        Effect blurShader, lightShader;

        float cameraAngleY = 120, cameraAngleX = 50; //camera rotation angles
        float lightAngleY = 20, lightAngleX = 160; //light rotation angles
        float lightDistance = 10;
        float distance = 400; //camera distance
        Vector3 cameraPosition, cameraTarget, lightPosition;

        Matrix world = Matrix.Identity;
        Matrix view = Matrix.CreateLookAt(
            new Vector3(0, 0, 20),
            new Vector3(0, 0, 0),
            Vector3.UnitY);
        Matrix projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(45), //field of view
            1024f / 768f,//aspect ratio
            0.1f, //near (e.g 0.1f)
            2000f); //far (e.g. 1000f)
        Matrix worldViewProjection = Matrix.Identity;
        Matrix preWorldViewProjection = Matrix.Identity; //WorldViewProjection from previous frame (used for blur)
        
        MouseState preMouse; //previous mouse state
        KeyboardState preKeyboard; //previous keyboard state
        Model[] models;
        Matrix[] modelTransform;
        Texture2D depthMap;
        Texture2D litScene;

        RenderTarget2D litSceneRenderTarget, depthMapRenderTarget; 

        Matrix lightView = Matrix.CreateLookAt(new Vector3(0, 0, 10), Vector3.Zero, Vector3.UnitY);
        Matrix lightProjection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.PiOver2, 1f, 1f, 100f
            );

        //For scene lighting
        Vector4 ambient = new Vector4(0.5f, 0.5f, 0.5f, 1f);
        Vector4 diffuseColor = new Vector4(0.5f, 0.5f, 0.5f, 1f);
        float diffuseIntensity = 1.0f;
        Vector4 specularColor = new Vector4(0.5f, 0.5f, 0.5f, 1f);
        float specularIntensity = 1.0f;
        float shininess = 40;

        bool drawBlurred = true; //flag to switch between blur/no-blur

        float preDeltaRotY = 0.0f;
        float preDeltaRotX = 0.0f;
        float preDeltaDistance = 0.0f;
        float preDeltaDown = 0.0f;
        float preDeltaRight = 0.0f;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            graphics.PreferredBackBufferHeight = 768;
            graphics.PreferredBackBufferWidth = 1024;
        }

        protected override void Initialize()
        {
            base.Initialize();
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
            font = Content.Load<SpriteFont>("Font");

            models = new Model[] {
                Content.Load<Model>("terrain/terrain"),
                Content.Load<Model>("wolf/Wolf")
            };

            modelTransform = new Matrix[]
            {
                //Matrix.Identity*Matrix.CreateScale(0.1f),
                Matrix.CreateTranslation(0,0,-50f),
                Matrix.Identity
            };

            blurShader = Content.Load<Effect>("BlurShader");
            lightShader = Content.Load<Effect>("PhongShader");

            litSceneRenderTarget = new RenderTarget2D(GraphicsDevice, Window.ClientBounds.Width, Window.ClientBounds.Height, false, SurfaceFormat.Color, DepthFormat.Depth24, 0, RenderTargetUsage.PlatformContents);

            depthMapRenderTarget = new RenderTarget2D(GraphicsDevice, Window.ClientBounds.Width, Window.ClientBounds.Height, false, SurfaceFormat.Color, DepthFormat.Depth24, 0, RenderTargetUsage.PlatformContents);
        }

        protected override void UnloadContent()
        {
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            //Switch between blur/no-blur with B key
            if(Keyboard.GetState().IsKeyUp(Keys.B) && preKeyboard.IsKeyDown(Keys.B)) this.drawBlurred = !this.drawBlurred;

            //Control light angles with Arrow Keys
            if (Keyboard.GetState().IsKeyDown(Keys.Left)) lightAngleY += 0.02f;
            if (Keyboard.GetState().IsKeyDown(Keys.Right)) lightAngleY -= 0.02f;
            if (Keyboard.GetState().IsKeyDown(Keys.Up)) lightAngleX += 0.02f;
            if (Keyboard.GetState().IsKeyDown(Keys.Down)) lightAngleX -= 0.02f;

            //Rotation:
            float deltaRotY = 0.0f;
            float deltaRotX = 0.0f;
            //Mouse Left button
            if (Mouse.GetState().LeftButton == ButtonState.Pressed)
            {
                deltaRotY -= (Mouse.GetState().X - preMouse.X) / 100f;
                deltaRotX += (Mouse.GetState().Y - preMouse.Y) / 100f;
            }
            //Keyboard: FTGH + shift/control to control speed
            if (Keyboard.GetState().IsKeyDown(Keys.F)) deltaRotY -= 0.05f;
            if (Keyboard.GetState().IsKeyDown(Keys.H)) deltaRotY += 0.05f;
            if (Keyboard.GetState().IsKeyDown(Keys.G)) deltaRotX += 0.05f;
            if (Keyboard.GetState().IsKeyDown(Keys.T)) deltaRotX -= 0.05f;
            
            //Make rotation faster if Control/Shift pressed
            if (Keyboard.GetState().IsKeyDown(Keys.LeftControl)){
                deltaRotX *= 2;
                deltaRotY *= 2;
            }
            if (Keyboard.GetState().IsKeyDown(Keys.LeftShift)){
                deltaRotX *= 2;
                deltaRotY *= 2;
            }
            cameraAngleX += deltaRotX + preDeltaRotX;
            cameraAngleY += deltaRotY + preDeltaRotY;
            //momentum
            preDeltaRotX += deltaRotX;
            preDeltaRotY += deltaRotY;
            preDeltaRotX /= 20.0f;
            preDeltaRotY /= 20.0f;

            //Zoom
            //Mouse Right button
            float deltaDistance = 0.0f;
            if (Mouse.GetState().RightButton == ButtonState.Pressed)
            {
                deltaDistance += (Mouse.GetState().X - preMouse.X) / 10f;
            }
            //Keyboard: Z (zoom out), X (zoom in)
            if (Keyboard.GetState().IsKeyDown(Keys.Z)) deltaDistance -= 10.0f;
            if (Keyboard.GetState().IsKeyDown(Keys.X)) deltaDistance += 10.0f;
            //Make zoom faster if Control/Shift pressed
            if (Keyboard.GetState().IsKeyDown(Keys.LeftControl)) deltaDistance *= 2;
            if (Keyboard.GetState().IsKeyDown(Keys.LeftShift)) deltaDistance *= 2;

            distance += deltaDistance + preDeltaDistance;
            //momentum
            preDeltaDistance += deltaDistance;
            preDeltaDistance /= 20.0f;
            if (preDeltaDistance < 0.1f) preDeltaDistance = 0.0f;

            //Translate (pan)
            float deltaRight = 0.0f;
            float deltaDown = 0.0f;
            //Mouse Middle Click
            if (Mouse.GetState().MiddleButton == ButtonState.Pressed)
            {
                deltaDown = (Mouse.GetState().Y - preMouse.Y) / 10f;
                deltaRight = (Mouse.GetState().X - preMouse.X) / 10f;
            }
            //Keyboard: AWSD
            if( Keyboard.GetState().IsKeyDown(Keys.A)) deltaRight += 1f;
            if (Keyboard.GetState().IsKeyDown(Keys.D)) deltaRight -= 1f;
            if (Keyboard.GetState().IsKeyDown(Keys.W)) deltaDown += 1f;
            if (Keyboard.GetState().IsKeyDown(Keys.S)) deltaDown -= 1f;
            //Make translation faster with shift/control pressed
            if (Keyboard.GetState().IsKeyDown(Keys.LeftShift))
            {
                deltaRight *= 2.0f;
                deltaDown *= 2.0f;
            }
            if (Keyboard.GetState().IsKeyDown(Keys.LeftControl))
            {
                deltaRight *= 2.0f;
                deltaDown *= 2.0f;
            }

            deltaRight += preDeltaRight;
            deltaDown += preDeltaDown;

            Vector3 ViewRight = Vector3.Transform(Vector3.UnitX,
                Matrix.CreateRotationX(cameraAngleX) * Matrix.CreateRotationY(cameraAngleY));
            Vector3 ViewUp = Vector3.Transform(Vector3.UnitY,
                Matrix.CreateRotationX(cameraAngleX) * Matrix.CreateRotationY(cameraAngleY));
            cameraTarget -= ViewRight * deltaRight;
            cameraTarget += ViewUp * deltaDown;

            //momentum for deltaRight/Down
            preDeltaDown = deltaDown / 20.0f;
            preDeltaRight = deltaRight / 20.0f;

            preWorldViewProjection = worldViewProjection; //previous view projection -- for blur

            //keep previous mouse/keyboard states
            preMouse = Mouse.GetState();
            preKeyboard = Keyboard.GetState();

            cameraPosition = Vector3.Transform(new Vector3(0, 0, distance),
                Matrix.CreateRotationX(cameraAngleX) * Matrix.CreateRotationY(cameraAngleY) * Matrix.CreateTranslation(cameraTarget));
            view = Matrix.CreateLookAt(
                cameraPosition,
                cameraTarget,
                Vector3.Transform(Vector3.UnitY, Matrix.CreateRotationX(cameraAngleX) * Matrix.CreateRotationY(cameraAngleY)));
            lightPosition = Vector3.Transform(
                new Vector3(0, 0, lightDistance),
                Matrix.CreateRotationX(lightAngleX) * Matrix.CreateRotationY(lightAngleY));
            lightView = Matrix.CreateLookAt(lightPosition, Vector3.Zero, Vector3.UnitY);

            worldViewProjection = world * view * projection;

            base.Update(gameTime);
        }

        private void DrawLitScene()
        {
            //Use the light shader to render the phong-shaded scene for use as a texture
            lightShader.CurrentTechnique = lightShader.Techniques[0];
            RasterizerState s = new RasterizerState();
            s.CullMode = CullMode.None;
            GraphicsDevice.RasterizerState = s;
            DepthStencilState ss = new DepthStencilState();
            //ss.DepthBufferFunction = CompareFunction.LessEqual;
            GraphicsDevice.DepthStencilState = ss;

            for(int i = 0; i < models.Length; i++)
            {
                Model model = models[i];
                foreach (EffectPass pass in lightShader.CurrentTechnique.Passes)
                {
                    foreach (ModelMesh mesh in model.Meshes)
                    {
                        foreach (ModelMeshPart part in mesh.MeshParts)
                        {
                            lightShader.Parameters["World"].SetValue(modelTransform[i] *  mesh.ParentBone.Transform);
                            lightShader.Parameters["View"].SetValue(view);
                            lightShader.Parameters["Projection"].SetValue(projection);
                            lightShader.Parameters["AmbientColor"].SetValue(ambient);
                            lightShader.Parameters["DiffuseColor"].SetValue(diffuseColor);
                            lightShader.Parameters["DiffuseIntensity"].SetValue(diffuseIntensity);
                            lightShader.Parameters["SpecularColor"].SetValue(specularColor);
                            lightShader.Parameters["SpecularIntensity"].SetValue(specularIntensity);
                            lightShader.Parameters["LightPosition"].SetValue(lightPosition);
                            lightShader.Parameters["CameraPosition"].SetValue(cameraPosition);
                            lightShader.Parameters["Shininess"].SetValue(shininess);

                            //Apply texture from model, if included
                            if (mesh.Effects.Count > 0)
                            {
                                Texture2D texture = (Texture2D)((BasicEffect)mesh.Effects[0]).Texture;
                                lightShader.Parameters["UVTexture"].SetValue(texture);
                            }

                            Matrix worldInverseTranspose = Matrix.Transpose(Matrix.Invert(mesh.ParentBone.Transform));
                            lightShader.Parameters["WorldInverseTranspose"].SetValue(worldInverseTranspose);

                            pass.Apply();
                            // set buffers and draw mesh model
                            GraphicsDevice.SetVertexBuffer(part.VertexBuffer);
                            GraphicsDevice.Indices = part.IndexBuffer;
                            GraphicsDevice.DrawIndexedPrimitives(
                               PrimitiveType.TriangleList,
                               part.VertexOffset,
                               part.StartIndex,
                               part.PrimitiveCount
                            );
                        }
                    }
                }
            }
        }

        private void DrawDepthMap()
        {
            //Draw depth map in texture for use with blur effect
            blurShader.CurrentTechnique = blurShader.Techniques[0]; //depthMap technique            
            for (int i = 0; i < models.Length; i++)
            {
                Model model = models[i];
                foreach (EffectPass pass in blurShader.CurrentTechnique.Passes)
                {
                    foreach (ModelMesh mesh in model.Meshes)
                    {
                        foreach (ModelMeshPart part in mesh.MeshParts)
                        {
                            blurShader.Parameters["WorldViewProjection"].SetValue( mesh.ParentBone.Transform *  view * projection );
                            Matrix worldInverseTransposeMatrix = Matrix.Transpose(Matrix.Invert(mesh.ParentBone.Transform));
                            blurShader.Parameters["WorldInverseTranspose"].SetValue(worldInverseTransposeMatrix);

                            pass.Apply();
                            GraphicsDevice.SetVertexBuffer(part.VertexBuffer);
                            GraphicsDevice.Indices = part.IndexBuffer;
                            GraphicsDevice.DrawIndexedPrimitives(
                                PrimitiveType.TriangleList,
                                part.VertexOffset,
                                part.StartIndex,
                                part.PrimitiveCount);
                        }
                    }
                }
            }
        }

        private void DrawBlurredScene()
        {
            //Draw the blurred scene (combines lit scene with information from depth map + previous World-View-Projection matrix)
            blurShader.CurrentTechnique = blurShader.Techniques[1]; //blur post-processing blurShader

            blurShader.Parameters["WorldViewProjection"].SetValue(worldViewProjection);
            blurShader.Parameters["InvWorldViewProjection"].SetValue(Matrix.Invert(worldViewProjection));

            blurShader.Parameters["preWorldViewProjection"].SetValue(preWorldViewProjection);
            blurShader.Parameters["preInvWorldViewProjection"].SetValue(Matrix.Invert(preWorldViewProjection));

            Matrix worldInverseTransposeMatrix = Matrix.Transpose(Matrix.Invert(Matrix.Identity));
            blurShader.Parameters["WorldInverseTranspose"].SetValue(worldInverseTransposeMatrix);

            blurShader.Parameters["DepthMap"].SetValue(this.depthMap);
            blurShader.Parameters["SceneTexture"].SetValue(this.litScene);

            blurShader.Parameters["isZoom"].SetValue(this.preDeltaDistance != 0.0f ? 1.0f : 0.0f);

            blurShader.CurrentTechnique.Passes[0].Apply();
            using (SpriteBatch spriteBatch = new SpriteBatch(GraphicsDevice))
            {
                //spriteBatch.Begin(0, null, null, null, null, blurShader);
                spriteBatch.Begin(SpriteSortMode.Texture, BlendState.Opaque, null, null, null, blurShader);
                spriteBatch.Draw(litScene, new Rectangle(0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height), Color.White);
                //spriteBatch.Draw(new Texture2D(graphics.GraphicsDevice, Window.ClientBounds.Width, Window.ClientBounds.Height), new Vector2(0, 0), Color.White);
                spriteBatch.End();
            }
       }
        

        private void DrawText()
        {
            //Draw some help text
            string[] text = new string[]
            {
                "CAMERA CONTROLS:",
                "Translation: W,A,S,D; Mouse Middle click",
                "Rotation: T,F,G,H; Mouse Left click",
                "Zoom in: X; Zoom out: X; Mouse Right click",
                "Pressing Ctrl/Shift increases movement speed by 2x. (4x with both)",
                "To enable/disable blur effect, press B [Currently " + (this.drawBlurred ? "Enabled" : "Disabled") + "]"
            };
            Vector2 pos = Vector2.Zero;
            float lineHeight = font.MeasureString(text[0]).Y;

            using (SpriteBatch spriteBatch = new SpriteBatch(GraphicsDevice))
            {
                spriteBatch.Begin(0, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.Default, null);

                for(int i = 0; i < text.Length; i++)
                {
                    spriteBatch.DrawString(font, text[i], pos, Color.White);
                    pos.Y += lineHeight;
                }
                spriteBatch.End();
            }
        }

        protected override void Draw(GameTime gameTime)
        {                        

            if (this.drawBlurred)
            {
                //Set render target for lit scene
                GraphicsDevice.SetRenderTarget(litSceneRenderTarget);
                GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.CornflowerBlue, 1.0f, 0);

                //Render Lit scene
                GraphicsDevice.DepthStencilState = new DepthStencilState();
                DrawLitScene();
                litScene = (Texture2D)litSceneRenderTarget;

                //Set render target for shadow map
                GraphicsDevice.SetRenderTarget(depthMapRenderTarget);
                GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Black, 1.0f, 0);

                //Render Shadow map
                GraphicsDevice.DepthStencilState = new DepthStencilState();
                DrawDepthMap();
                depthMap = (Texture2D)depthMapRenderTarget;

                //Set render target to screen
                GraphicsDevice.SetRenderTarget(null);
                //Clear the render target
                GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.White, 1.0f, 0);
                //Draw Blurred Scene
                DrawBlurredScene();
                
            }
            else //Draw lit scene, no blurring
            {
                //Set render target for lit scene
                GraphicsDevice.SetRenderTarget(null);
                GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.CornflowerBlue, 1.0f, 0);

                //Render Lit scene
                GraphicsDevice.BlendState = BlendState.Opaque;
                GraphicsDevice.DepthStencilState = new DepthStencilState();
                DrawLitScene();
            }

            DrawText();

            base.Draw(gameTime);
        }
    }
}
