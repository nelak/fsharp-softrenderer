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

open System
open System.Windows
open SharpDX
open System.Windows.Media
open System.Windows.Media.Imaging
open SoftRenderer
open FSharpx

type MainWindow = XAML<"Window.xaml">

let window = MainWindow()

type MainWindowModel() = 

    let Device = Device(Size2((int)window.Root.Width, (int)window.Root.Height))

    let Meshes = Device.LoadMeshes("monkey.babylon")

    let Camera = Camera(Vector3(0.0f, 0.0f, 10.0f), Vector3.Zero)

    let Bitmap = WriteableBitmap((int)window.Root.Width, (int)window.Root.Height, 96.0, 96.0, PixelFormats.Bgra32, null)

    let mutable previousDate = DateTime.Now

    member this.BitMap = Bitmap
    member this.CompositionTarget_Rendering(e: EventArgs) =
        // Fps
        let now = DateTime.Now
        let currentFps = 1000.0 / (now - previousDate).TotalMilliseconds
        previousDate <- now

        window.fps.Text <- String.Format("{0:0.00} fps", currentFps)

        //Rendering loop
        Device.Clear(0uy, 0uy, 0uy, 255uy)

        for mesh in Meshes do 
            // rotating slightly the cube during each frame rendered
//            mesh.Rotation <- new Vector3(mesh.Rotation.X + 0.01f, mesh.Rotation.Y + 0.01f, mesh.Rotation.Z);
            mesh.Rotation <- new Vector3(mesh.Rotation.X, mesh.Rotation.Y + 0.01f, mesh.Rotation.Z);

            // Doing the various matrix operations
            Device.Render(Camera, mesh)

            // Flushing the back buffer into the front buffer
            Bitmap.WritePixels(new Int32Rect(0, 0, Device.Size.Width, Device.Size.Height), Device.BackBuffer, (int) Device.Size.Width * 4, 0)
        ()

[<STAThread>]
[<EntryPoint>]
let main(_) = 
    let model = MainWindowModel()
    window.FrontBuffer.Source <- model.BitMap
    CompositionTarget.Rendering.Add(model.CompositionTarget_Rendering)
    (new Application()).Run(window.Root)
