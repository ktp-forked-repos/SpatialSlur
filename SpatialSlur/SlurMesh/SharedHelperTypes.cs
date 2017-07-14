﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/*
 * Notes
 * 
 * Collection of small POD types used in specific algorithms.
 */ 

namespace SpatialSlur.SlurMesh
{
    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public struct SplitDisjointHandle
    {
        /// <summary>
        /// The index of the component to which the corresponding element belongs.
        /// </summary>
        public int ComponentIndex;


        /// <summary>
        /// The index of the corresponding edge within the component.
        /// </summary>
        public int EdgeIndex;


        /// <summary>
        /// 
        /// </summary>
        public SplitDisjointHandle(int componentIndex = -1, int edgeIndex = -1)
        {
            ComponentIndex = componentIndex;
            EdgeIndex = edgeIndex;
        }
    }
}
