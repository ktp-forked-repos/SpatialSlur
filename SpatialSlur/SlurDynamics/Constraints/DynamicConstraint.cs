﻿using System;
using System.Collections.Generic;
using System.Linq;
using SpatialSlur.SlurCore;

/*
 * Notes
 */

namespace SpatialSlur.SlurDynamics
{
    /// <summary>
    /// Base class for constraints on a dynamic collection of particles.
    /// </summary>
    [Serializable]
    public abstract class DynamicConstraint<H> : IConstraint
        where H : BodyHandle
    {
        private List<H> _handles;
        private double _weight;


        /// <summary>
        /// 
        /// </summary>
        public List<H> Handles
        {
            get { return _handles; }
        }


        /// <summary>
        /// 
        /// </summary>
        public double Weight
        {
            get { return _weight; }
            set
            {
                if (value < 0.0)
                    throw new ArgumentOutOfRangeException("Weight cannot be negative.");

                _weight = value;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        public bool AppliesRotation
        {
            get { return true; }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="weight"></param>
        public DynamicConstraint(double weight = 1.0)
        {
            _handles = new List<H>();
            Weight = weight;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="capacity"></param>
        /// <param name="weight"></param>
        public DynamicConstraint(int capacity, double weight = 1.0)
        {
            _handles = new List<H>(capacity);
            Weight = weight;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="handles"></param>
        /// <param name="weight"></param>
        public DynamicConstraint(IEnumerable<H> handles, double weight = 1.0)
        {
            _handles = new List<H>(handles);
            Weight = weight;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="particles"></param>
        public abstract void Calculate(IReadOnlyList<IBody> particles);


        /// <summary>
        /// 
        /// </summary>
        /// <param name="particles"></param>
        public void Apply(IReadOnlyList<IBody> particles)
        {
            foreach (var h in _handles)
            {
                var p = particles[h];
                p.ApplyMove(h.Delta, Weight);
                p.ApplyRotate(h.AngleDelta, Weight);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="indices"></param>
        public void SetHandles(IEnumerable<int> indices)
        {
            var itr = indices.GetEnumerator();

            foreach (var h in _handles)
            {
                h.Index = itr.Current;
                itr.MoveNext();
            }
        }
    }
}
