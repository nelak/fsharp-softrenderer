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
    type ScanLineData =
            struct
                val mutable CurrentY: int
                val mutable Ndotla: float32
                val mutable Ndotlb: float32
                val mutable Ndotlc: float32
                val mutable Ndotld: float32
                val mutable ua: float32
                val mutable ub: float32
                val mutable uc: float32
                val mutable ud: float32
                val mutable va: float32
                val mutable vb: float32
                val mutable vc: float32
                val mutable vd: float32
                //new(currentY: int, ndotla: float32, ndotlb: float32, ndotlc: float32, ndotld:float32) = { CurrentY = currentY; Ndotla = ndotla; Ndotlb = ndotlb; Ndotlc= ndotlc; Ndotld = ndotld }
            end

