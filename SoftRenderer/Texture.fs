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
open Microsoft.FSharp.Control
open System.Windows.Media.Imaging
open System
open System.IO
open SharpDX

    // Working with a fix sized texture (512x512, 1024x1024, etc.).
    [<AllowNullLiteral>]
    type Texture(filename: string, width: int, height: int) =
        let mutable internalBuffer: byte[] = null
        let load(filename: string) =
            async {
                    let location = Path.Combine(Environment.CurrentDirectory, filename)
                    use! stream = File.AsyncOpenRead(location)
                    let bmp = new BitmapImage()
                    bmp.BeginInit()
                    bmp.CacheOption <- BitmapCacheOption.OnLoad
                    bmp.StreamSource <- stream
                    bmp.EndInit()
                    let buffer = Array.create (bmp.PixelHeight * bmp.PixelWidth * 4) 0uy
                    bmp.CopyPixels(buffer, width * 4, 0)
                    internalBuffer <- buffer
            } |> Async.Start

        do
            load(filename)

        member this.Filename = filename
        member this.Width = width
        member this.Height = height

        // Takes the U & V coordinates exported by Blender
        // and return the corresponding pixel color in the texture
        member this.Map(tu: float32, tv: float32) =
            // Image is not loaded yet
            if (internalBuffer = null) then Color4.White
            else
                // using a % operator to cycle/repeat the texture if needed
                let u = Math.Abs((int) (tu*(float32)this.Width) % this.Width)
                let v = Math.Abs((int) (tv*(float32)this.Height) % this.Height)

                let pos = (u + v * width) * 4
                let b = internalBuffer.[pos]
                let g = internalBuffer.[pos + 1]
                let r = internalBuffer.[pos + 2]
                let a = internalBuffer.[pos + 3]

                Color4((float32)r / 255.0f, (float32)g / 255.0f, (float32)b / 255.0f, (float32)a / 255.0f)


