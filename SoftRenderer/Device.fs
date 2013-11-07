// Copyright 2013 Gaston Cababie
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace SoftRenderer
open SharpDX
open System
open System.Globalization
open System.Threading.Tasks
open FSharp.Data
open Microsoft.FSharp.Control
open System.IO

open Operators
    type MeshParser = JsonProvider<"monkey.babylon">

    type Device(size: Size2) =
        let bufferSize = (size.Width * size.Height * 4)
        let depthBuffer = Array.create (size.Width * size.Height) Single.MaxValue
        let backBuffer = Array.create bufferSize 0uy

        //http://rosettacode.org/wiki/Bitmap/Bresenham%27s_line_algorithm#F.23
        //TODO: Implement myself
        let bresenham fill (x0, y0) (x1, y1) =
          let steep = abs(y1 - y0) > abs(x1 - x0)
          let x0, y0, x1, y1 =
            if steep then y0, x0, y1, x1 else x0, y0, x1, y1
          let x0, y0, x1, y1 =
            if x0 > x1 then x1, y1, x0, y0 else x0, y0, x1, y1
          let dx, dy = x1 - x0, abs(y1 - y0)
          let s = if y0 < y1 then 1 else -1
          let rec loop e x y =
            if x <= x1 then
              if steep then fill y x else fill x y
              if e < dy then
                loop (e-dy+dx) (x+1) (y+s)
              else
                loop (e-dy) (x+1) y
          loop (dx/2) x0 y0

        member this.Size = size

        member this.BackBuffer = backBuffer

        member this.DepthBuffer = depthBuffer

        member this.LoadMeshes(filename: string) =
            async {
                let location = Path.Combine(Environment.CurrentDirectory, filename)
                use! stream = File.AsyncOpenText(location)
                let! json = stream.ReadToEndAsync() |> Async.AwaitTask
                let jsonMesh = MeshParser.Parse(json)
                let createMesh(mesh: MeshParser.DomainTypes.Mesh) : Mesh = 
                    let verticesStep =
                        match mesh.UvCount with
                        | 0 -> 6
                        | 1 -> 8
                        | 2 -> 10
                        | _ -> -1

                    let vertexCount = mesh.Vertices.Length / verticesStep
                    let facesCount = mesh.Indices.Length / 3
                    let vertices = [for i = 0 to vertexCount - 1 do
                                        let textureCoordinates = 
                                            if (mesh.UvCount > 0) then
                                                // Loading the texture coordinates
                                                let u = (float32)mesh.Vertices.[i * verticesStep + 6]
                                                let v = (float32)mesh.Vertices.[i * verticesStep + 7]
                                                Vector2(u, v)
                                            else 
                                                Vector2.Zero
                                        yield Vertex(Vector3((float32)mesh.Vertices.[i * verticesStep], (float32)mesh.Vertices.[i * verticesStep + 1], (float32)mesh.Vertices.[i * verticesStep + 2]), Vector3((float32)mesh.Vertices.[i * verticesStep + 3], (float32)mesh.Vertices.[i * verticesStep + 4], (float32)mesh.Vertices.[i * verticesStep + 5]), Vector3.Zero, textureCoordinates)] |> List.toArray
                    let faces = [for i = 0 to facesCount - 1 do
                                        yield Face(mesh.Indices.[i * 3], mesh.Indices.[i * 3 + 1], mesh.Indices.[i * 3 + 2])] |> List.toArray

                    let materials = [for i = 0 to jsonMesh.Materials.Length - 1 do
                                        let material = Material()
                                        material.Name <- jsonMesh.Materials.[i].Name
                                        material.Id <- jsonMesh.Materials.[i].Id
                                        if (jsonMesh.Materials.[i].DiffuseTexture <> null) then
                                            material.DiffuseTextureName <- jsonMesh.Materials.[i].DiffuseTexture.Name
                                        yield material] |> Seq.map (fun material -> material.Id, material) |> Map.ofSeq
                
                    let texture  = 
                        if (mesh.UvCount > 0) then
                            // Texture
                            let meshTextureID = (string)mesh.MaterialId
                            let meshTextureName = materials.[meshTextureID].DiffuseTextureName
                            Texture(meshTextureName, 512, 512)
                        else 
                            null

                    let loadedMesh = Mesh(mesh.Name, vertices, faces)
                    loadedMesh.Texture <- texture
                    loadedMesh

                let meshes = [ for mesh in jsonMesh.Meshes -> createMesh(mesh) ] |> List.toArray
                return meshes
            } |> Async.RunSynchronously

        member this.Clear(b: byte, r: byte, g:byte, a:byte) = 
            let mutable i = 0
            while i < (bufferSize - 4) do
                this.BackBuffer.[i] <- b
                this.BackBuffer.[i + 1] <- g
                this.BackBuffer.[i + 2] <- r
                this.BackBuffer.[i + 3] <- a
                i <- i+4
            
            // Clearing Depth Buffer
            let mutable index = 0
            while index < this.DepthBuffer.Length - 1 do
                this.DepthBuffer.[index] <- Single.MaxValue
                index <- index + 1

        member this.PutPixel(x:int, y:int, z:float32, color:Color4) = 
            let index = (x + y * this.Size.Width)
            let index4 = index * 4

            if (this.DepthBuffer.[index] < z) then ()
            else
                this.DepthBuffer.[index] <- z

                this.BackBuffer.[index4] <- (byte)(color.Blue * 255.0f)
                this.BackBuffer.[index4+1] <- (byte)(color.Green * 255.0f)
                this.BackBuffer.[index4+2] <- (byte)(color.Red * 255.0f)
                this.BackBuffer.[index4+3] <- (byte)(color.Alpha * 255.0f)
        
        // Project takes some 3D coordinates and transform them
        // in 2D coordinates using the transformation matrix
        // It also transform the same coordinates and the norma to the vertex 
        // in the 3D world
        member this.Project(vertex: Vertex, transform: Matrix, worldMatrix: Matrix) =
            // transforming the coordinates into 2D space
            let point2d = Vector3.TransformCoordinate(vertex.Coordinates, transform)

            // transforming the coordinates & the normal to the vertex in the 3D world
            let point3dWorld = Vector3.TransformCoordinate(vertex.Coordinates, worldMatrix)
            let normal3dWorld = Vector3.TransformCoordinate(vertex.Normal, worldMatrix)

            // The transformed coordinates will be based on coordinate system
            // starting on the center of the screen. But drawing on screen normally starts
            // from top left. We then need to transform them again to have x:0, y:0 on top left.
            let x = point2d.X * (float32)this.Size.Width + (float32)this.Size.Width / 2.0f 
            let y = - point2d.Y * (float32)this.Size.Height + (float32)this.Size.Height / 2.0f

            Vertex(Vector3(x, y, point2d.Z), normal3dWorld, point3dWorld, vertex.TextureCoordinates)


        // DrawPoint calls PutPixel but does the clipping operation before
        member this.DrawPoint(point: Vector3, color: Color4) =
            if (point.X >= 0.0f && point.Y >= 0.0f && point.X < (float32)this.Size.Width && point.Y < (float32)this.Size.Height) then 
                this.PutPixel((int)point.X, (int)point.Y, point.Z, color)

        member this.Render(camera: Camera, [<ParamArray>] meshes: Mesh[]) =
            // To understand this part, please read the prerequisites resources
            let viewMatrix = Matrix.LookAtLH(camera.Position, camera.Target, Vector3.UnitY)
            let projectionMatrix = Matrix.PerspectiveFovLH(0.78f, (float32)this.Size.Width / (float32)this.Size.Height, 0.01f, 1.0f)
            for mesh in meshes do
                let worldMatrix = Matrix.RotationYawPitchRoll(mesh.Rotation.Y, mesh.Rotation.X, mesh.Rotation.Z) * Matrix.Translation(mesh.Position)
                let transformMatrix = worldMatrix * viewMatrix * projectionMatrix

                Parallel.For(0, mesh.Faces.Length, fun faceIndex -> 
                    
                    let face = mesh.Faces.[faceIndex]

                    let vertexA = mesh.Vertices.[face.A]
                    let vertexB = mesh.Vertices.[face.B]
                    let vertexC = mesh.Vertices.[face.C]
                    
                    let pixelA = ref (this.Project(vertexA, transformMatrix, worldMatrix))
                    let pixelB = ref (this.Project(vertexB, transformMatrix, worldMatrix))
                    let pixelC = ref (this.Project(vertexC, transformMatrix, worldMatrix))

                    let color = 1.0f
                    this.DrawTriangle(pixelA, pixelB, pixelC, Color4(color, color, color, 1.0f), mesh.Texture)
                    ()) |> ignore


        // Clamping values to keep them between 0 and 1
        member this.Clamp(value:float32, ?min: float32, ?max: float32) =
            let min = defaultArg min 0.0f
            let max = defaultArg max 1.0f
            Math.Max(min, Math.Min(value, max))

            // Interpolating the value between 2 vertices 
            // min is the starting point, max the ending point
            // and gradient the % between the 2 points
        member this.Interpolate(min:float32, max:float32, gradient: float32) =
            min + (max - min) * this.Clamp(gradient)

        // drawing line between 2 points from left to right
        // papb -> pcpd
        // pa, pb, pc, pd must then be sorted before
        member this.ProcessScanLine(data: ScanLineData , va: Vertex , vb: Vertex , vc: Vertex , vd: Vertex , color: Color4, texture: Texture) =

            let pa = va.Coordinates
            let pb = vb.Coordinates
            let pc = vc.Coordinates
            let pd = vd.Coordinates

            // Thanks to current Y, we can compute the gradient to compute others values like
            // the starting X (sx) and ending X (ex) to draw between
            // if pa.Y == pb.Y or pc.Y == pd.Y, gradient is forced to 1
            let gradient1 = if pa.Y <> pb.Y then ((float32)data.CurrentY - pa.Y) / (pb.Y - pa.Y) else 1.0f
            let gradient2 = if pc.Y <> pd.Y then ((float32)data.CurrentY - pc.Y) / (pd.Y - pc.Y) else 1.0f
            
            let sx = (int) (this.Interpolate(pa.X, pb.X, gradient1))
            let ex = (int) (this.Interpolate(pc.X, pd.X, gradient2))

            // starting Z & ending Z
            let z1 = this.Interpolate(pa.Z, pb.Z, gradient1)
            let z2 = this.Interpolate(pc.Z, pd.Z, gradient2)

            // Interpolating normals on Y
            let snl = this.Interpolate(data.Ndotla, data.Ndotlb, gradient1)
            let enl = this.Interpolate(data.Ndotlc, data.Ndotld, gradient2)

            // Interpolating texture coordinates on Y
            let su = this.Interpolate(data.ua, data.ub, gradient1)
            let eu = this.Interpolate(data.uc, data.ud, gradient2)
            let sv = this.Interpolate(data.va, data.vb, gradient1)
            let ev = this.Interpolate(data.vc, data.vd, gradient2)


            let mutable x = sx
            while x < ex do
                
                let gradient = (float32) (x - sx) / (float32)(ex - sx)
                let z = this.Interpolate(z1, z2, gradient)
                let ndotl = this.Interpolate(snl, enl, gradient)
                let u = this.Interpolate(su, eu, gradient)
                let v = this.Interpolate(sv, ev, gradient)


                let textureColor =
                    if (texture <> null) then
                        texture.Map(u, v)
                    else
                        Color4(1.0f, 1.0f, 1.0f, 1.0f)

                // changing the color value using the cosine of the angle
                // between the light vector and the normal vector

                this.DrawPoint(Vector3((float32)x, (float32)data.CurrentY, z), color * ndotl* textureColor)
                x <- x + 1



        // Compute the cosine of the angle between the light vector and the normal vector
        // Returns a value between 0 and 1
        member this.ComputeNDotL(vertex: Vector3, normal: Vector3, lightPosition: Vector3) =
            let lightDirection = lightPosition - vertex

            normal.Normalize()
            lightDirection.Normalize()

            Math.Max(0.0f, Vector3.Dot(normal, lightDirection))

        member this.DrawTriangle(v1: byref<Vertex>, v2: byref<Vertex>, v3: byref<Vertex>, color: Color4, texture: Texture) =
            // Sorting the points in order to always have this order on screen p1, p2 & p3
            // with p1 always up (thus having the Y the lowest possible to be near the top screen)
            // then p2 between p1 & p3
            if (v1.Coordinates.Y > v2.Coordinates.Y) then 
                let temp = v2
                v2 <- v1
                v1 <- temp

            if (v2.Coordinates.Y > v3.Coordinates.Y) then
                let temp = v2
                v2 <- v3
                v3 <- temp

            if (v1.Coordinates.Y > v2.Coordinates.Y) then
                let temp = v2
                v2 <- v1
                v1 <- temp


            let p1 = v1.Coordinates;
            let p2 = v2.Coordinates;
            let p3 = v3.Coordinates;

            // normal face's vector is the average normal between each vertex's normal
            // computing also the center point of the face
            let vnFace = (v1.Normal + v2.Normal + v3.Normal) / 3.0f
            let centerPoint = (v1.WorldCoordinates + v2.WorldCoordinates + v3.WorldCoordinates) / 3.0f
            // Light position 
            let lightPos = Vector3(0.0f, 10.0f, 10.0f)
            // computing the cos of the angle between the light vector and the normal vector
            // it will return a value between 0 and 1 that will be used as the intensity of the color
            let nl1 = this.ComputeNDotL(v1.WorldCoordinates, v1.Normal, lightPos)
            let nl2 = this.ComputeNDotL(v2.WorldCoordinates, v2.Normal, lightPos)
            let nl3 = this.ComputeNDotL(v3.WorldCoordinates, v3.Normal, lightPos)

            let mutable data = ScanLineData()

            // http://en.wikipedia.org/wiki/Slope
            // Computing inverse slopes
            let dP1P2 = if (p2.Y - p1.Y > 0.0f) then
                            (p2.X - p1.X) / (p2.Y - p1.Y)
                        else
                             0.0f

            let dP1P3 = if (p3.Y - p1.Y > 0.0f) then
                            (p3.X - p1.X) / (p3.Y - p1.Y)
                        else
                             0.0f

            // First case where triangles are like that:
            // P1
            // -
            // -- 
            // - -
            // -  -
            // -   - P2
            // -  -
            // - -
            // -
            // P3
            if (dP1P2 > dP1P3) then
                let mutable y = (int)p1.Y
                while y <= (int)p3.Y do
                    data.CurrentY <- y
                    if ((float32)y < p2.Y) then
                        data.Ndotla <- nl1
                        data.Ndotlb <- nl3
                        data.Ndotlc <- nl1
                        data.Ndotld <- nl2

                        data.ua <- v1.TextureCoordinates.X
                        data.ub <- v3.TextureCoordinates.X
                        data.uc <- v1.TextureCoordinates.X
                        data.ud <- v2.TextureCoordinates.X
                                
                        data.va <- v1.TextureCoordinates.Y
                        data.vb <- v3.TextureCoordinates.Y
                        data.vc <- v1.TextureCoordinates.Y
                        data.vd <- v2.TextureCoordinates.Y

                        this.ProcessScanLine(data, v1, v3, v1, v2, color, texture)
                    else
                        data.Ndotla <- nl1
                        data.Ndotlb <- nl3
                        data.Ndotlc <- nl2
                        data.Ndotld <- nl3

                        data.ua <- v1.TextureCoordinates.X
                        data.ub <- v3.TextureCoordinates.X
                        data.uc <- v2.TextureCoordinates.X
                        data.ud <- v3.TextureCoordinates.X
                                
                        data.va <- v1.TextureCoordinates.Y
                        data.vb <- v3.TextureCoordinates.Y
                        data.vc <- v2.TextureCoordinates.Y
                        data.vd <- v3.TextureCoordinates.Y

                        this.ProcessScanLine(data, v1, v3, v2, v3, color, texture)
                    y <- y + 1

            // First case where triangles are like that:
            //       P1
            //        -
            //       -- 
            //      - -
            //     -  -
            // P2 -   - 
            //     -  -
            //      - -
            //        -
            //       P3
            else
                let mutable y = (int)p1.Y
                while (y <= (int)p3.Y) do
                    data.CurrentY <- y
                    if ((float32)y < p2.Y) then
                        data.Ndotla <- nl1
                        data.Ndotlb <- nl2
                        data.Ndotlc <- nl1
                        data.Ndotld <- nl3

                        data.ua <- v1.TextureCoordinates.X
                        data.ub <- v2.TextureCoordinates.X
                        data.uc <- v1.TextureCoordinates.X
                        data.ud <- v3.TextureCoordinates.X

                        data.va <- v1.TextureCoordinates.Y
                        data.vb <- v2.TextureCoordinates.Y
                        data.vc <- v1.TextureCoordinates.Y
                        data.vd <- v3.TextureCoordinates.Y

                        this.ProcessScanLine(data, v1, v2, v1, v3, color, texture)
                    else
                        data.Ndotla <- nl2
                        data.Ndotlb <- nl3
                        data.Ndotlc <- nl1
                        data.Ndotld <- nl3

                        data.ua <- v2.TextureCoordinates.X
                        data.ub <- v3.TextureCoordinates.X
                        data.uc <- v1.TextureCoordinates.X
                        data.ud <- v3.TextureCoordinates.X

                        data.va <- v2.TextureCoordinates.Y
                        data.vb <- v3.TextureCoordinates.Y
                        data.vc <- v1.TextureCoordinates.Y
                        data.vd <- v3.TextureCoordinates.Y

                        this.ProcessScanLine(data, v2, v3, v1, v3, color, texture)
                    y <- y + 1