﻿// 
// Radegast Metaverse Client
// Copyright (c) 2009-2011, Radegast Development Team
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 
//     * Redistributions of source code must retain the above copyright notice,
//       this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright
//       notice, this list of conditions and the following disclaimer in the
//       documentation and/or other materials provided with the distribution.
//     * Neither the name of the application "Radegast", nor the names of its
//       contributors may be used to endorse or promote products derived from
//       this software without specific prior CreateReflectionTexture permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
// CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//
// $Id$
//

using System;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL;
using OpenMetaverse;
using OpenMetaverse.Rendering;

namespace Radegast.Rendering
{
    /// <summary>
    /// Contains per primitive face data
    /// </summary>
    public class FaceData : IDisposable
    {
        public float[] Vertices;
        public ushort[] Indices;
        public float[] TexCoords;
        public float[] Normals;
        public int PickingID = -1;
        public int VertexVBO = -1;
        public int IndexVBO = -1;
        public TextureInfo TextureInfo = new TextureInfo();
        public BoundingVolume BoundingVolume = new BoundingVolume();
        public static int VertexSize = 32; // sizeof (vertex), 2  x vector3 + 1 x vector2 = 8 floats x 4 bytes = 32 bytes 
        public TextureAnimationInfo AnimInfo;
        public int QueryID = 0;
        public bool VBOFailed = false;

        /// <summary>
        /// Dispose VBOs if we have them in graphics card memory
        /// </summary>
        public void Dispose()
        {
            if (VertexVBO != -1) Compat.DeleteBuffer(VertexVBO);
            if (IndexVBO != -1) Compat.DeleteBuffer(IndexVBO);
            VertexVBO = -1;
            IndexVBO = -1;
        }

        /// <summary>
        /// Checks if VBOs are created, if they are, bind them, if not create new
        /// </summary>
        /// <param name="face">Which face's mesh is uploaded in this VBO</param>
        /// <returns>True, if face data was succesfully uploaded to the graphics card memory</returns>
        public bool CheckVBO(Face face)
        {
            if (VertexVBO == -1)
            {
                Vertex[] vArray = face.Vertices.ToArray();
                Compat.GenBuffers(out VertexVBO);
                Compat.BindBuffer(BufferTarget.ArrayBuffer, VertexVBO);
                Compat.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(vArray.Length * VertexSize), vArray, BufferUsageHint.StaticDraw);
                if (Compat.BufferSize(BufferTarget.ArrayBuffer) != vArray.Length * VertexSize)
                {
                    VBOFailed = true;
                    Compat.BindBuffer(BufferTarget.ArrayBuffer, 0);
                    Compat.DeleteBuffer(VertexVBO);
                    VertexVBO = -1;
                    return false;
                }
            }

            if (IndexVBO == -1)
            {
                ushort[] iArray = face.Indices.ToArray();
                Compat.GenBuffers(out IndexVBO);
                Compat.BindBuffer(BufferTarget.ElementArrayBuffer, IndexVBO);
                Compat.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(iArray.Length * sizeof(ushort)), iArray, BufferUsageHint.StaticDraw);
                if (Compat.BufferSize(BufferTarget.ElementArrayBuffer) != iArray.Length * sizeof(ushort))
                {
                    VBOFailed = true;
                    Compat.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
                    Compat.DeleteBuffer(IndexVBO);
                    IndexVBO = -1;
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Class handling texture animations
    /// </summary>
    public class TextureAnimationInfo
    {
        public Primitive.TextureAnimation PrimAnimInfo;
        public float CurrentFrame;
        public float CurrentTime;
        public bool PingPong;
        float LastTime = 0f;
        float TotalTime = 0f;

        /// <summary>
        /// Perform texture manupulation to implement texture animations
        /// </summary>
        /// <param name="lastFrameTime">Time passed since the last run (in seconds)</param>
        public void Step(float lastFrameTime)
        {
            float numFrames = 1f;
            float fullLength = 1f;

            if (PrimAnimInfo.Length > 0)
            {
                numFrames = PrimAnimInfo.Length;
            }
            else
            {
                numFrames = Math.Max(1f, (float)(PrimAnimInfo.SizeX * PrimAnimInfo.SizeY));
            }

            if ((PrimAnimInfo.Flags & Primitive.TextureAnimMode.PING_PONG) != 0)
            {
                if ((PrimAnimInfo.Flags & Primitive.TextureAnimMode.SMOOTH) != 0)
                {
                    fullLength = 2f * numFrames;
                }
                else if ((PrimAnimInfo.Flags & Primitive.TextureAnimMode.LOOP) != 0)
                {
                    fullLength = 2f * numFrames - 2f;
                    fullLength = Math.Max(1f, fullLength);
                }
                else
                {
                    fullLength = 2f * numFrames - 1f;
                    fullLength = Math.Max(1f, fullLength);
                }
            }
            else
            {
                fullLength = numFrames;
            }

            float frameCounter;
            if ((PrimAnimInfo.Flags & Primitive.TextureAnimMode.SMOOTH) != 0)
            {
                frameCounter = lastFrameTime * PrimAnimInfo.Rate + LastTime;
            }
            else
            {
                TotalTime += lastFrameTime;
                frameCounter = TotalTime * PrimAnimInfo.Rate;
            }
            LastTime = frameCounter;

            if ((PrimAnimInfo.Flags & Primitive.TextureAnimMode.LOOP) != 0)
            {
                frameCounter %= fullLength;
            }
            else
            {
                frameCounter = Math.Min(fullLength - 1f, frameCounter);
            }

            if ((PrimAnimInfo.Flags & Primitive.TextureAnimMode.SMOOTH) == 0)
            {
                frameCounter = (float)Math.Floor(frameCounter + 0.01f);
            }

            if ((PrimAnimInfo.Flags & Primitive.TextureAnimMode.PING_PONG) != 0)
            {
                if (frameCounter > numFrames)
                {
                    if ((PrimAnimInfo.Flags & Primitive.TextureAnimMode.SMOOTH) != 0)
                    {
                        frameCounter = numFrames - (frameCounter - numFrames);
                    }
                    else
                    {
                        frameCounter = (numFrames - 1.99f) - (frameCounter - numFrames);
                    }
                }
            }

            if ((PrimAnimInfo.Flags & Primitive.TextureAnimMode.REVERSE) != 0)
            {
                if ((PrimAnimInfo.Flags & Primitive.TextureAnimMode.SMOOTH) != 0)
                {
                    frameCounter = numFrames - frameCounter;
                }
                else
                {
                    frameCounter = (numFrames - 0.99f) - frameCounter;
                }
            }

            frameCounter += PrimAnimInfo.Start;

            if ((PrimAnimInfo.Flags & Primitive.TextureAnimMode.SMOOTH) == 0)
            {
                frameCounter = (float)Math.Round(frameCounter);
            }


            GL.MatrixMode(MatrixMode.Texture);
            GL.LoadIdentity();

            if ((PrimAnimInfo.Flags & Primitive.TextureAnimMode.ROTATE) != 0)
            {
                GL.Translate(0.5f, 0.5f, 0f);
                GL.Rotate(Utils.RAD_TO_DEG * frameCounter, OpenTK.Vector3d.UnitZ);
                GL.Translate(-0.5f, -0.5f, 0f);
            }
            else if ((PrimAnimInfo.Flags & Primitive.TextureAnimMode.SCALE) != 0)
            {
                GL.Scale(frameCounter, frameCounter, 0);
            }
            else // Translate
            {
                float sizeX = Math.Max(1f, (float)PrimAnimInfo.SizeX);
                float sizeY = Math.Max(1f, (float)PrimAnimInfo.SizeY);

                GL.Scale(1f / sizeX, 1f / sizeY, 0);
                GL.Translate(frameCounter % sizeX, Math.Floor(frameCounter / sizeY), 0);
            }

            GL.MatrixMode(MatrixMode.Modelview);
        }

        [Obsolete("Use Step() instead")]
        public void ExperimentalStep(float time)
        {
            int reverseFactor = 1;
            float rate = PrimAnimInfo.Rate;

            if (rate < 0)
            {
                rate = -rate;
                reverseFactor = -reverseFactor;
            }

            if ((PrimAnimInfo.Flags & Primitive.TextureAnimMode.REVERSE) != 0)
            {
                reverseFactor = -reverseFactor;
            }

            CurrentTime += time;
            float totalTime = 1 / rate;

            uint x = Math.Max(1, PrimAnimInfo.SizeX);
            uint y = Math.Max(1, PrimAnimInfo.SizeY);
            uint nrFrames = x * y;

            if (PrimAnimInfo.Length > 0 && PrimAnimInfo.Length < nrFrames)
            {
                nrFrames = (uint)PrimAnimInfo.Length;
            }

            GL.MatrixMode(MatrixMode.Texture);
            GL.LoadIdentity();

            if (CurrentTime >= totalTime)
            {
                CurrentTime = 0;
                CurrentFrame++;
                if (CurrentFrame > nrFrames) CurrentFrame = (uint)PrimAnimInfo.Start;
                if ((PrimAnimInfo.Flags & Primitive.TextureAnimMode.PING_PONG) != 0)
                {
                    PingPong = !PingPong;
                }
            }

            float smoothOffset = 0f;

            if ((PrimAnimInfo.Flags & Primitive.TextureAnimMode.SMOOTH) != 0)
            {
                smoothOffset = (CurrentTime / totalTime) * reverseFactor;
            }

            float f = CurrentFrame;
            if (reverseFactor < 0)
            {
                f = nrFrames - CurrentFrame;
            }

            if ((PrimAnimInfo.Flags & Primitive.TextureAnimMode.ROTATE) == 0) // not rotating
            {
                GL.Scale(1f / x, 1f / y, 0f);
                GL.Translate((f % x) + smoothOffset, f / y, 0);
            }
            else
            {
                smoothOffset = (CurrentTime * PrimAnimInfo.Rate);
                float startAngle = PrimAnimInfo.Start;
                float endAngle = PrimAnimInfo.Length;
                float angle = startAngle + (endAngle - startAngle) * smoothOffset;
                GL.Translate(0.5f, 0.5f, 0f);
                GL.Rotate(Utils.RAD_TO_DEG * angle, OpenTK.Vector3d.UnitZ);
                GL.Translate(-0.5f, -0.5f, 0f);
            }

            GL.MatrixMode(MatrixMode.Modelview);
        }

    }

    /// <summary>
    /// Class that handle rendering of objects: simple primitives, scupties, and meshes
    /// </summary>
    public class RenderPrimitive : SceneObject
    {
        #region Public fields
        /// <summary>Base simulator object</summary>
        public Primitive Prim;
        public List<Face> Faces;
        /// <summary>Is this object attached to an avatar</summary>
        public bool Attached;
        /// <summary>Do we know if object is attached</summary>
        public bool AttachedStateKnown;
        /// <summary>Are meshes constructed and ready for this prim</summary>
        public bool Meshed;
        /// <summary>Process of creating a mesh is underway</summary>
        public bool Meshing;
        #endregion Public fields

        #region Private fields
        int prevTEHash;
        int prevSculptHash;
        int prevShapeHash;
        #endregion Private fields

        /// <summary>
        /// Default constructor
        /// </summary>
        public RenderPrimitive()
        {
            Type = SceneObjectType.Primitive;
        }

        /// <summary>
        /// Remove any GL resource we may still have in use
        /// </summary>
        public override void Dispose()
        {
            if (Faces != null)
            {
                foreach (Face f in Faces)
                {
                    if (f.UserData != null && f.UserData is FaceData)
                    {
                        FaceData data = (FaceData)f.UserData;
                        data.Dispose();
                        data = null;
                    }
                }
                Faces = null;
            }
            base.Dispose();
        }

        /// <summary>
        /// Simulator object that is basis for this SceneObject
        /// </summary>
        public override Primitive BasePrim
        {
            get { return Prim; }
            set
            {
                Prim = value;

                int TEHash = Prim.Textures == null ? 0 : Prim.Textures.GetHashCode();
                int sculptHash, shapeHash;

                if (Meshed)
                {
                    if (Prim.Type == PrimType.Sculpt || Prim.Type == PrimType.Mesh)
                    {
                        sculptHash = Prim.Sculpt.GetHashCode();
                        if (Prim.Sculpt.GetHashCode() != prevSculptHash || TEHash != prevTEHash)
                        {
                            Meshed = false;
                        }
                        prevSculptHash = sculptHash;
                    }
                    else
                    {
                        shapeHash = Prim.PrimData.GetHashCode();
                        if (shapeHash != prevShapeHash)
                        {
                            Meshed = false;
                        }
                        else if (TEHash != prevTEHash)
                        {
                            Meshed = false;
                        }
                        prevShapeHash = shapeHash;
                    }
                }
                prevTEHash = TEHash;
            }
        }

        /// <summary>
        /// Set initial state of the object
        /// </summary>
        public override void Initialize()
        {
            AttachedStateKnown = false;
            base.Initialize();
        }

        /// <summary>
        /// Render Primitive
        /// </summary>
        /// <param name="pass">Which pass are we currently in</param>
        /// <param name="pickingID">ID used to identify which object was picked</param>
        /// <param name="scene">Main scene renderer</param>
        /// <param name="time">Time it took to render the last frame</param>
        public override void Render(RenderPass pass, int pickingID, SceneWindow scene, float time)
        {
            if (!RenderSettings.AvatarRenderingEnabled && Attached) return;

            // Individual prim matrix
            GL.PushMatrix();

            // Prim roation and position and scale
            GL.MultMatrix(Math3D.CreateSRTMatrix(Prim.Scale, RenderRotation, RenderPosition));

            // Do we have animated texture on this face
            bool animatedTexture = false;

            // Initialise flags tracking what type of faces this prim has
            if (pass == RenderPass.Simple)
            {
                HasSimpleFaces = false;
            }
            else if (pass == RenderPass.Alpha)
            {
                HasAlphaFaces = false;
            }
            else if (pass == RenderPass.Invisible)
            {
                HasInvisibleFaces = false;
            }

            // Draw the prim faces
            for (int j = 0; j < Faces.Count; j++)
            {
                Primitive.TextureEntryFace teFace = Prim.Textures.GetFace((uint)j);
                Face face = Faces[j];
                FaceData data = (FaceData)face.UserData;

                if (data == null)
                    continue;

                if (teFace == null)
                    continue;

                // Don't render transparent faces
                Color4 RGBA = teFace.RGBA;

                if (data.TextureInfo.FullAlpha || RGBA.A <= 0.01f) continue;

                bool switchedLightsOff = false;

                if (pass == RenderPass.Picking)
                {
                    data.PickingID = pickingID;
                    var primNrBytes = Utils.UInt16ToBytes((ushort)pickingID);
                    var faceColor = new byte[] { primNrBytes[0], primNrBytes[1], (byte)j, 255 };
                    GL.Color4(faceColor);
                }
                else if (pass == RenderPass.Invisible)
                {
                    if (!data.TextureInfo.IsInvisible) continue;
                    HasInvisibleFaces = true;
                }
                else
                {
                    if (data.TextureInfo.IsInvisible) continue;
                    bool belongToAlphaPass = (RGBA.A < 0.99f) || (data.TextureInfo.HasAlpha && !data.TextureInfo.IsMask);

                    if (belongToAlphaPass && pass != RenderPass.Alpha) continue;
                    if (!belongToAlphaPass && pass == RenderPass.Alpha) continue;

                    if (pass == RenderPass.Simple)
                    {
                        HasSimpleFaces = true;
                    }
                    else if (pass == RenderPass.Alpha)
                    {
                        HasAlphaFaces = true;
                    }

                    if (teFace.Fullbright)
                    {
                        GL.Disable(EnableCap.Lighting);
                        switchedLightsOff = true;
                    }

                    float shiny = 0f;
                    switch (teFace.Shiny)
                    {
                        case Shininess.High:
                            shiny = 0.96f;
                            break;

                        case Shininess.Medium:
                            shiny = 0.64f;
                            break;

                        case Shininess.Low:
                            shiny = 0.24f;
                            break;
                    }

                    if (shiny > 0f)
                    {
                        scene.StartShiny();
                    }
                    GL.Material(MaterialFace.Front, MaterialParameter.Shininess, shiny);
                    var faceColor = new float[] { RGBA.R, RGBA.G, RGBA.B, RGBA.A };
                    GL.Color4(faceColor);

                    GL.Material(MaterialFace.Front, MaterialParameter.Specular, new float[] { 0.5f, 0.5f, 0.5f, 1f });

                    if (data.TextureInfo.TexturePointer == 0)
                    {
                        TextureInfo teInfo;
                        if (scene.TryGetTextureInfo(teFace.TextureID, out teInfo))
                        {
                            data.TextureInfo = teInfo;
                        }
                    }

                    if (data.TextureInfo.TexturePointer == 0)
                    {
                        GL.Disable(EnableCap.Texture2D);
                        if (!data.TextureInfo.FetchFailed)
                        {
                            scene.DownloadTexture(new TextureLoadItem()
                            {
                                Prim = this.Prim,
                                TeFace = teFace,
                                Data = data
                            });
                        }
                    }
                    else
                    {
                        // Is this face using texture animation
                        if ((Prim.TextureAnim.Flags & Primitive.TextureAnimMode.ANIM_ON) != 0
                            && (Prim.TextureAnim.Face == j || Prim.TextureAnim.Face == 255))
                        {
                            if (data.AnimInfo == null)
                            {
                                data.AnimInfo = new TextureAnimationInfo();
                            }
                            data.AnimInfo.PrimAnimInfo = Prim.TextureAnim;
                            data.AnimInfo.Step(time);
                            animatedTexture = true;
                        }
                        else if (data.AnimInfo != null) // Face texture not animated. Do we have previous anim setting?
                        {
                            data.AnimInfo = null;
                        }

                        GL.Enable(EnableCap.Texture2D);
                        GL.BindTexture(TextureTarget.Texture2D, data.TextureInfo.TexturePointer);
                    }
                }

                if (!RenderSettings.UseVBO || data.VBOFailed)
                {
                    Vertex[] verts = face.Vertices.ToArray();
                    ushort[] indices = face.Indices.ToArray();

                    unsafe
                    {
                        fixed (float* normalPtr = &verts[0].Normal.X)
                        fixed (float* texPtr = &verts[0].TexCoord.X)
                        {
                            GL.NormalPointer(NormalPointerType.Float, FaceData.VertexSize, (IntPtr)normalPtr);
                            GL.TexCoordPointer(2, TexCoordPointerType.Float, FaceData.VertexSize, (IntPtr)texPtr);
                            GL.VertexPointer(3, VertexPointerType.Float, FaceData.VertexSize, verts);
                            GL.DrawElements(BeginMode.Triangles, indices.Length, DrawElementsType.UnsignedShort, indices);
                        }
                    }
                }
                else
                {
                    if (data.CheckVBO(face))
                    {
                        Compat.BindBuffer(BufferTarget.ArrayBuffer, data.VertexVBO);
                        Compat.BindBuffer(BufferTarget.ElementArrayBuffer, data.IndexVBO);
                        GL.NormalPointer(NormalPointerType.Float, FaceData.VertexSize, (IntPtr)12);
                        GL.TexCoordPointer(2, TexCoordPointerType.Float, FaceData.VertexSize, (IntPtr)(24));
                        GL.VertexPointer(3, VertexPointerType.Float, FaceData.VertexSize, (IntPtr)(0));

                        GL.DrawElements(BeginMode.Triangles, face.Indices.Count, DrawElementsType.UnsignedShort, IntPtr.Zero);
                    }
                    Compat.BindBuffer(BufferTarget.ArrayBuffer, 0);
                    Compat.BindBuffer(BufferTarget.ElementArrayBuffer, 0);

                }

                if (switchedLightsOff)
                {
                    GL.Enable(EnableCap.Lighting);
                    switchedLightsOff = false;
                }

            }

            GL.BindTexture(TextureTarget.Texture2D, 0);
            RHelp.ResetMaterial();

            // Reset texture coordinates if we modified them in texture animation
            if (animatedTexture)
            {
                GL.MatrixMode(MatrixMode.Texture);
                GL.LoadIdentity();
                GL.MatrixMode(MatrixMode.Modelview);
            }

            // Pop the prim matrix
            GL.PopMatrix();

            base.Render(pass, pickingID, scene, time);
        }

        /// <summary>
        /// String representation of the object
        /// </summary>
        /// <returns>String containing local ID of the object and it's distance from the camera</returns>
        public override string ToString()
        {
            uint id = Prim == null ? 0 : Prim.LocalID;
            float distance = (float)Math.Sqrt(DistanceSquared);
            return string.Format("LocalID: {0}, distance {0.00}", id, distance);
        }
    }
}
