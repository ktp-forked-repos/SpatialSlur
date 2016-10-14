﻿ using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpatialSlur.SlurCore;

/*
 * Notes
 * 
 * TODO refactor as per Field3d 
 */
  
namespace SpatialSlur.SlurField
{
    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public abstract class Field2d
    {
        private Domain2d _domain;
        private double _x0, _y0; // cached for convenience

        private FieldBoundaryType _boundaryType;
        private double _dx, _dy;
        private readonly int _nx, _ny, _n;

        // inverse values cached to avoid unecessary divs
        private double _dxInv, _dyInv;
        private readonly double _nxInv;

        // delegates for methods which depend on the field's boundary type
        private delegate void ToIndex2(Vec2d point, out int i, out int j);
        private ToIndex2 _index2At;
        private Action<Vec2d, FieldPoint2d> _fieldPointAt;


        //
        private Field2d(int countX, int countY)
        {
            if (countX < 2 || countY < 2)
                throw new System.ArgumentException("The field must have 2 or more values in each dimension.");

            _nx = countX;
            _ny = countY;
            _n = _nx * _ny;

            _nxInv = 1.0 / _nx;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="countX"></param>
        /// <param name="countY"></param>
        /// <param name="boundaryType"></param>
        protected Field2d(Domain2d domain, int countX, int countY, FieldBoundaryType boundaryType = FieldBoundaryType.Equal)
            : this(countX, countY)
        {
            Domain = domain;
            BoundaryType = boundaryType;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="other"></param>
        protected Field2d(Field2d other)
        {
            _nx = other._nx;
            _ny = other._ny;
            _n = other._n;

            _nxInv = other._nxInv;

            Domain = other._domain;
            BoundaryType = other._boundaryType;
        }


        /// <summary>
        /// Gets/sets the domain of the field.
        /// </summary>
        public Domain2d Domain
        {
            get { return _domain; }
            set
            {
                if (!value.IsValid)
                    throw new System.ArgumentException("The given domain must be valid.");

                _domain = value;
                OnDomainChange();
            }
        }


        /// <summary>
        /// Returns the number of values in the field.
        /// </summary>
        public int Count
        {
            get { return _n; }
        }
  

        /// <summary>
        /// Returns the number of values in the x direction.
        /// </summary>
        public int CountX
        {
            get { return _nx; }
        }


        /// <summary>
        /// Returns the number of values in the y direction.
        /// </summary>
        public int CountY
        {
            get { return _ny; }
        }


        /// <summary>
        /// Returns the distance between values in the x direction.
        /// </summary>
        public double ScaleX
        {
            get { return _dx; }
        }


        /// <summary>
        /// Returns the distance between values in the y direction.
        /// </summary>
        public double ScaleY
        {
            get { return _dy; }
        }


        /// <summary>
        /// Gets/sets the boundary type for the field.
        /// This property determines how edge cases are handled in many other methods.
        /// </summary>
        public FieldBoundaryType BoundaryType
        {
            get { return _boundaryType; }
            set 
            { 
                _boundaryType = value;
                OnBoundaryTypeChange();
            }
        }


        /// <summary>
        /// Iterates through the coordinates of each value in the field. 
        /// Note that these are calculated on the fly and not explicitly stored in memory.
        /// If you need to cache them, call GetCoordinates instead.
        /// </summary>
        public IEnumerable<Vec2d> Coordinates
        {
            get
            {
                for (int j = 0; j < _ny; j++)
                {
                    for (int i = 0; i < _nx; i++)
                    {
                        yield return CoordinateAt(i,j);
                    }
                }
            }
        }


        /// <summary>
        /// This is called after any changes to the field's domain.
        /// </summary>
        protected virtual void OnDomainChange()
        {
            _x0 = _domain.x.t0;
            _y0 = _domain.y.t0;

            _dx = _domain.x.Span / (_nx - 1);
            _dy = _domain.y.Span / (_ny - 1);

            _dxInv = 1.0 / _dx;
            _dyInv = 1.0 / _dy;
        }


        /// <summary>
        /// This is called after any changes to the field's boundary type.
        /// </summary>
        protected virtual void OnBoundaryTypeChange()
        {
            switch(_boundaryType)
            {
                case FieldBoundaryType.Constant:
                    {
                        _index2At = Index2AtConstant;
                        _fieldPointAt = FieldPointAtConstant;
                        break;
                    }
                case FieldBoundaryType.Equal:
                    {
                        _index2At = Index2AtEqual;
                        _fieldPointAt = FieldPointAtEqual;
                        break;
                    }
                case FieldBoundaryType.Periodic:
                    {
                        _index2At = Index2AtPeriodic;
                        _fieldPointAt = FieldPointAtPeriodic;
                        break;
                    }
            }
        }


        /// <summary>
        /// Returns coordinates of all values in the field.
        /// </summary>
        /// <returns></returns>
        public Vec2d[] GetCoordinates()
        {
            Vec2d[] result = new Vec2d[_n];
            GetCoordinates(result);
            return result;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="coords"></param>
        public void GetCoordinates(IList<Vec2d> coords)
        {
            Parallel.ForEach(Partitioner.Create(0, _n), range =>
            {
                int i, j;
                ExpandIndex(range.Item1, out i, out j);

                for (int index = range.Item1; index < range.Item2; index++, i++)
                {
                    if (i == _nx) { j++; i = 0; }
                    coords[index] = CoordinateAt(i, j);
                }
            });
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="index2"></param>
        /// <returns></returns>
        public Vec2d CoordinateAt(Vec2i index2)
        {
            return CoordinateAt(index2.x, index2.y);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <returns></returns>
        public Vec2d CoordinateAt(int i, int j)
        {
            return new Vec2d(
                i * _dx + _x0,
                j * _dy + _y0);
        }


        /// <summary>
        /// Returns true if the field has the same number of values in each dimension as another.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool ResolutionEquals(Field2d other)
        {
            return (_nx == other._nx && _ny == other._ny);
        }


        /// <summary>
        /// Returns true if the field contains the given 2 dimensional index.
        /// </summary>
        /// <param name="index2"></param>
        /// <returns></returns>
        public bool ContainsIndex(Vec2i index2)
        {
            return ContainsIndex(index2.x, index2.y);
        }


        /// <summary>
        /// Returns true if the field contains the given 2 dimensional index.
        /// </summary>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <returns></returns>
        public bool ContainsIndex(int i, int j)
        {
            return i >= 0 && j >= 0 && i < _nx && j < _ny;
        }


        /// <summary>
        /// Converts a 2 dimensional index into a 1 dimensional index.
        /// </summary>
        /// <param name="index2"></param>
        /// <returns></returns>
        public int FlattenIndex(Vec2i index2)
        {
            return FlattenIndex(index2.x, index2.y);
        }


        /// <summary>
        /// Converts a 2 dimensional index into a 1 dimensional index.
        /// </summary>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <returns></returns>
        public int FlattenIndex(int i, int j)
        {
            return i + j * _nx;
        }


        /// <summary>
        /// Converts a 1 dimensional index into a 2 dimensional index.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public Vec2i ExpandIndex(int index)
        {
            int i, j;
            ExpandIndex(index, out i, out j);
            return new Vec2i(i, j);
        }


        /// <summary>
        /// Converts a 1 dimensional index into a 2 dimensional index.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="i"></param>
        /// <param name="j"></param>
        public void ExpandIndex(int index, out int i, out int j)
        {
            j = (int)(index * _nxInv);
            i = index - j * _nx;
        }
      

        /// <summary>
        /// Returns the index of the value nearest to the given point.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public int IndexAt(Vec2d point)
        {
            int i, j;
            _index2At(point, out i, out j);
            return FlattenIndex(i, j);
        }


        /// <summary>
        /// Returns the index of the value nearest to the given point.
        /// Assumes the given point is inside the field domain.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public int IndexAtUnchecked(Vec2d point)
        {
            int i, j;
            Index2AtUnchecked(point, out i, out j);
            return FlattenIndex(i, j);
        }


        /// <summary>
        /// Returns the 2 dimensional index of the value nearest to the given point.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public Vec2i Index2At(Vec2d point)
        {
            int i, j;
            _index2At(point, out i, out j);
            return new Vec2i(i, j);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="point"></param>
        /// <param name="i"></param>
        /// <param name="j"></param>
        public void Index2At(Vec2d point, out int i, out int j)
        {
            _index2At(point, out i, out j);
        }


        /// <summary>
        /// Returns the 2 dimensional index of the value nearest to the given point.
        /// Assumes the given point is inside the field domain.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public Vec2i Index2AtUnchecked(Vec2d point)
        {
            int i, j;
            Index2AtUnchecked(point, out i, out j);
            return new Vec2i(i, j);
        }


        /// <summary>
        /// Returns the 2 dimensional index of the value nearest to the given point.
        /// Assumes the given point is inside the field domain.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="i"></param>
        /// <param name="j"></param>
        public void Index2AtUnchecked(Vec2d point, out int i, out int j)
        {
            i = (int)Math.Round((point.x - _x0) * _dxInv);
            j = (int)Math.Round((point.y - _y0) * _dyInv);
        }


        /// <summary>
        /// 
        /// </summary>
        private void Index2AtConstant(Vec2d point, out int i, out int j)
        {
            Index2AtUnchecked(point, out i, out j);

            // set to 2d index of boundary value if out of bounds
            if(!ContainsIndex(i,j))
            {
                i = _nx;
                j = _ny;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        private void Index2AtEqual(Vec2d point, out int i, out int j)
        {
            Index2AtUnchecked(point, out i, out j);

            i = SlurMath.Clamp(i, _nx - 1);
            j = SlurMath.Clamp(j, _ny - 1);
        }


        /// <summary>
        /// 
        /// </summary>
        private void Index2AtPeriodic(Vec2d point, out int i, out int j)
        {
            Index2AtUnchecked(point, out i, out j);

            i = SlurMath.Mod2(i, _nx);
            j = SlurMath.Mod2(j, _ny);
        }


        /// <summary>
        /// Returns indices and weights of the 4 values nearest to the given point.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public FieldPoint2d FieldPointAt(Vec2d point)
        {
            FieldPoint2d result = new FieldPoint2d();
            _fieldPointAt(point, result);
            return result;
        }


        /// <summary>
        /// Returns indices and weights of the 4 values nearest to the given point.
        /// Assumes the given point is inside the field domain.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public FieldPoint2d FieldPointAtUnchecked(Vec2d point)
        {
            FieldPoint2d result = new FieldPoint2d();
            FieldPointAtUnchecked(point, result);
            return result;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="point"></param>
        /// <param name="result"></param>
        public void FieldPointAt(Vec2d point, FieldPoint2d result)
        {
            _fieldPointAt(point, result);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="point"></param>
        /// <param name="result"></param>
        public void FieldPointAtUnchecked(Vec2d point, FieldPoint2d result)
        {
            int i, j;

            // set weights with fractional components
            result.SetWeights(
             SlurMath.Fract((point.x - _x0) * _dxInv, out i),
             SlurMath.Fract((point.y - _y0) * _dyInv, out j));

            // set corner indices
            int index = FlattenIndex(i, j);
            var corners = result.Corners;
            corners[0] = index;
            corners[1] = index + 1;
            corners[2] = index + _nx;
            corners[3] = index + 1 + _nx;
        }


        /// <summary>
        /// 
        /// </summary> 
        private void FieldPointAtConstant(Vec2d point, FieldPoint2d result)
        {
            int i, j;

            // set weights with fractional components
            result.SetWeights(
             SlurMath.Fract((point.x - _x0) * _dxInv, out i),
             SlurMath.Fract((point.y - _y0) * _dyInv, out j));

            // bit mask (0 = in bounds, 1 = out of bounds)
            int mask = 0;
            if (!SlurMath.Contains(i, 0, _nx)) mask |= 1;
            if (!SlurMath.Contains(j, 0, _ny)) mask |= 2;
            if (!SlurMath.Contains(i + 1, 0, _nx)) mask |= 4;
            if (!SlurMath.Contains(j + 1, 0, _ny)) mask |= 8;

            // set corner indices
            int index = FlattenIndex(i, j);
            var corners = result.Corners;
            corners[0] = ((mask & 3) == 0) ? index : _n; // 00 11
            corners[1] = ((mask & 6) == 0) ? index + 1 : _n; // 01 10
            corners[2] = ((mask & 9) == 0) ? index + _nx : _n; // 10 01
            corners[3] = ((mask & 12) == 0) ? index + 1 + _nx : _n; // 11 00
        }


        /// <summary>
        /// 
        /// </summary>
        private void FieldPointAtEqual(Vec2d point, FieldPoint2d result)
        {
            int i, j;

            // set weights with fractional components
            result.SetWeights(
             SlurMath.Fract((point.x - _x0) * _dxInv, out i),
             SlurMath.Fract((point.y - _y0) * _dyInv, out j));

            // clamp and get offsets
            int di = 0, dj = 0;

            if (i < 0) i = 0;
            else if (i > _nx - 1) i = _nx - 1;
            else if (i < _nx - 1) di = 1;

            if (j < 0) j = 0;
            else if (j > _ny - 1) j = _ny - 1;
            else if (j < _ny - 1) dj = _nx;

            // set corner indices
            int index = FlattenIndex(i, j);
            var corners = result.Corners;
            corners[0] = index;
            corners[1] = index + di;
            corners[2] = index + dj;
            corners[3] = index + di + dj;
        }


        /// <summary>
        /// 
        /// </summary>
        private void FieldPointAtPeriodic(Vec2d point, FieldPoint2d result)
        {
            int i, j;

            // set weights with fractional components
            result.SetWeights(
             SlurMath.Fract((point.x - _x0) * _dxInv, out i),
             SlurMath.Fract((point.y - _y0) * _dyInv, out j));

            // wrap whole components
            i = SlurMath.Mod2(i, _nx);
            j = SlurMath.Mod2(j, _ny);

            // get offsets
            int di = (i == _nx - 1) ? 1 - _nx : 1;
            int dj = (j == _ny - 1) ? _nx - _n : _nx;

            // set corner indices
            int index = FlattenIndex(i, j);
            var corners = result.Corners;
            corners[0] = index;
            corners[1] = index + di;
            corners[2] = index + dj;
            corners[3] = index + di + dj;
        }


        /// <summary>
        /// 
        /// </summary>
        private void SetWeights(FieldPoint2d fieldPoint, Vec2d point, out int i, out int j)
        {
            fieldPoint.SetWeights(
                SlurMath.Fract((point.x - _x0) * _dxInv, out i),
                SlurMath.Fract((point.y - _y0) * _dyInv, out j));
        }


        /*
        // Alternative implementation better suited to more involved types of interpolation (cubic etc.)
        // http://paulbourke.net/miscellaneous/interpolation/
        private void FieldPointAtClamped2(Vec2d point, FieldPoint2d result)
        {
            // convert to grid space and separate fractional and whole components
            int i0, j0;
            double u = SlurMath.Fract((point.x - _x0) * _dxInv, out i0);
            double v = SlurMath.Fract((point.y - _y0) * _dyInv, out j0);

            int[] corners = result.Corners;
            int index = 0;

            for (int j = 0; j < 2; j++)
            {
                int jj = SlurMath.Clamp(j0 + j, _ny - 1);
                for (int i = 0; i < 2; i++)
                {
                    int ii = SlurMath.Clamp(i0 + i, _nx - 1);
                    corners[index] = FlattenIndex(ii, jj);
                    index++;
                }
            }

            // compute weights using fractional components
            result.SetWeights(u, v);
        }
        */
    }
}
