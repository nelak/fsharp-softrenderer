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
    //let inline (!>) arg = ( ^a : (static member op_Implicit : ^a -> ^b) arg)

    type Mesh(name: string, vertices: Vertex[], faces: Face[]) =
        member this.Name = name
        member this.Vertices = vertices
        member this.Faces = faces
        member val Position = Vector3.Zero with get
        member val Rotation = Vector3.Zero with get, set
        member val Texture: Texture = null with get, set        

