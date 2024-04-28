using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ClipperLib;
using g3;
using goa.Common;
using NetTopologySuite.Geometries;

namespace PublicProjectMethods_
{
    using cInt = Int64;
    using Location = Autodesk.Revit.DB.Location;
    using Path = List<IntPoint>;
    using Paths = List<List<IntPoint>>;

    public static class _g3To
    {
        #region ToRevit

        public static IEnumerable<CurveLoop> ToCurveLoops(this IEnumerable<Polygon2d> polygon2ds)
        {
            foreach (var item in polygon2ds)
            {
                yield return item.ToCurveLoop();
            }
        }

        public static CurveLoop ToCurveLoop(this Polygon2d _polygon2d)
        {
            _polygon2d = _polygon2d.DelDuplicate();
            List<Line> lines = new List<Line>();
            foreach (var seg in _polygon2d.SegmentItr())
            {
                lines.Add(seg.ToLine());
            }
            CurveLoop curves = new CurveLoop();
            foreach (Line line in lines)
            {
                Curve _c = line as Curve;
                curves.Append(_c);
            }
            return curves;
        }

        public static Arc ToArc(this Arc2d arc2d)
        {
            XYZ center = arc2d.Center.ToXYZ();
            double radius = arc2d.Radius;
            double AngleStartDeg = (Math.PI / 180) * arc2d.AngleStartDeg;
            double AngleEndDeg = (Math.PI / 180) * arc2d.AngleEndDeg;
            XYZ xAxis = new XYZ(1, 0, 0);
            XYZ yAxis = new XYZ(0, 1, 0);

            return Arc.Create(center, radius, AngleStartDeg, AngleEndDeg, xAxis, yAxis);
        }

        public static IEnumerable<Line> ToLines(this IEnumerable<Segment2d> segment2ds)
        {
            foreach (var item in segment2ds)
                yield return item.ToLine();
        }

        public static Line ToLine(this Segment2d segment2D)
        {
            return Line.CreateBound(segment2D.P0.ToXYZ(), segment2D.P1.ToXYZ());
        }

        public static Line ToLineUnBound(this Segment2d segment2D)
        {
            return Line.CreateUnbound(segment2D.P0.ToXYZ(), segment2D.P1.ToXYZ());
        }

        public static IEnumerable<List<XYZ>> ToXyzses(this IEnumerable<IEnumerable<Vector2d>> vector2dses)
        {
            foreach (var item in vector2dses)
            {
                yield return item.ToXyzs().ToList();
            }
        }

        public static IEnumerable<XYZ> ToXyzs(this IEnumerable<Vector2d> vector2ds)
        {
            foreach (var v in vector2ds)
                yield return v.ToXYZ();
        }

        public static IEnumerable<XYZ> ToXyzs(this IEnumerable<Vector3d> vector3ds)
        {
            foreach (var v in vector3ds)
                yield return v.ToXYZ();
        }

        public static XYZ ToXYZ(this Vector2d vector2D)
        {
            return new XYZ(vector2D.x, vector2D.y, 0);
        }

        public static XYZ ToXYZ(this Vector3d vector3D)
        {
            return new XYZ(vector3D.x, vector3D.y, vector3D.z);
        }

        #endregion ToRevit

        #region ToClipper

        public static Paths ToPaths(this IEnumerable<List<Vector2d>> vector2Dses)
        {
            Paths _Paths = new Paths();
            foreach (List<Vector2d> vector2Ds in vector2Dses)
            {
                Path _Path = vector2Ds.ToPath();
                _Paths.Add(_Path);
            }
            return _Paths;
        }

        public static Path ToPath(this IEnumerable<Vector2d> vector2Ds)
        {
            Path _Path = new Path();
            foreach (Vector2d vector2d in vector2Ds)
            {
                IntPoint _IntPoint = vector2d.ToIntPoint();
                _Path.Add(_IntPoint);
            }
            return _Path;
        }

        /// <summary>
        /// g3 vector2d to clipper intpoint
        /// </summary>
        public static IntPoint ToIntPoint(this Vector2d vector2D)
        {
            double x = vector2D.x * Precision_.clipperMultiple;
            double y = vector2D.y * Precision_.clipperMultiple;
            return new IntPoint((cInt)x, (cInt)y);
        }

        #endregion ToClipper

        #region ToNetTopologySuite

        public static Polygon ToPolygon(this Polygon2d polygon2d)
        {
            ICollection<Vector2d> vector2ds = polygon2d.Vertices;
            return vector2ds.ToPolygon();
        }

        public static Polygon ToPolygon(this IEnumerable<Vector2d> vector2ds)
        {
            IEnumerable<Coordinate> coordinates = vector2ds.ToCoordinates();
            Coordinate[] _coordinates = coordinates.ToArray();
            //【】LinearRing的构造点集，必要闭合的首尾点
            _coordinates = _coordinates.Append(_coordinates.First()).ToArray();
            LinearRing linearRing = new LinearRing(_coordinates);
            return new Polygon(linearRing);
        }

        public static IEnumerable<Coordinate> ToCoordinates(this IEnumerable<Vector2d> vector2ds)
        {
            foreach (var item in vector2ds)
            {
                yield return item.ToCoordinate();
            }
        }

        public static Coordinate ToCoordinate(this Vector2d vector2d)
        {
            return new Coordinate(vector2d.x, vector2d.y);
        }

        #endregion ToNetTopologySuite
    }

    public static class WallExtend
    {
        /// <summary>
        /// 获取墙体在平面的轮廓（以LocationCurve为基准长度）
        /// </summary>
        /// <param name="wall"></param>
        /// <returns></returns>
        public static List<Line> GetPlaneBorders(this Wall wall)
        {
            Curve curveExt = wall.GetCurveExt();
            double wallThick = wall.GetWallThick();
            Curve curve = curveExt.OffsetVector(wall.Orientation * wallThick * 0.5);
            Curve curve2 = curveExt.OffsetVector(wall.Orientation * -(wallThick * 0.5));
            List<XYZ> list = new List<XYZ>();
            list.Add(curve.StartPoint());
            list.Add(curve.EndPoint());
            list.Add(curve2.StartPoint());
            list.Add(curve2.EndPoint());
            list.SortCounterClockwise();
            return list.GetCurveLoop();
        }

        public static PlanarFace GetWallBottomFace(this Wall wall, UIApplication app)
        {
            var opt = app.Application.Create.NewGeometryOptions();
            var e = wall.get_Geometry(opt);
            const double _tolerance = 0.001;
            foreach (var obj in e)
            {
                var solid = obj as Solid;
                if (null == solid) continue;
                foreach (Face face in solid.Faces)
                {
                    var pf = face as PlanarFace;
                    if (null == pf) continue;
                    if (pf.FaceNormal.IsVertical( _tolerance) && pf.FaceNormal.Z < 0)
                    {
                        return pf;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 获取墙的底部轮廓
        /// </summary>
        /// <param name="wall"></param>
        /// <param name="uiapp"></param>
        /// <returns></returns>
        public static List<CurveLoop> GetWallBottomProfile(this Wall wall, UIApplication uiapp)
        {
            var profiles = new List<CurveLoop>();
            if (wall == null) return profiles;
            var planarFace=wall.GetWallBottomFace(uiapp);
            if (planarFace == null) return profiles;

            //墙的底部截面profile
            profiles = planarFace.GetEdgesAsCurveLoops().ToList();

            return profiles;
        }

        /// <summary>
        /// 厚度
        /// </summary>
        /// <param name="wall"></param>
        /// <returns></returns>
        public static double GetWallThick(this Wall wall)
        {
            return wall.WallType.GetWallThick();
        }

        /// <summary>
        /// 厚度
        /// </summary>
        /// <param name="wallType"></param>
        /// <returns></returns>
        public static double GetWallThick(this WallType wallType)
        {
            return wallType.Width;
        }
    }

    public static class ElementExtend
    {
        /// <summary>
        /// 获取图元的定位线
        /// </summary>
        /// <param name="elem"></param>
        /// <returns></returns>
        public static Curve GetCurveExt(this Element elem)
        {
            Curve curve;
            if (elem is Grid)
            {
                curve = (elem as Grid).Curve;
            }
            else
            {
                curve = elem.Location.GetCurve();
            }
            return curve;
        }
    }

    public static class LocationExtend
    {
        /// <summary>
        /// 定位点
        /// </summary>
        /// <param name="loc"></param>
        /// <param name="dRotate"></param>
        /// <returns></returns>
        public static XYZ GetPoint(this Location loc, out double dRotate)
        {
            dRotate = 0;
            if (!(loc is LocationPoint)) return null;
            LocationPoint lp = loc as LocationPoint;
            try
            {
                dRotate = lp.Rotation;//MEP中有一些族获取此参数时抛出不明异常
            }
            catch (Exception ex)
            {
                //CBIMLog.LogException(ex);
            }
            return lp.Point;
        }

        /// <summary>
        /// 定位点
        /// </summary>
        /// <param name="loc"></param>
        /// <returns></returns>
        public static XYZ GetPoint(this Location loc)
        {
            return loc.GetPoint(out _);
        }

        /// <summary>
        /// 转角度
        /// </summary>
        /// <param name="loc"></param>
        /// <returns></returns>
        public static double GetRotation(this Location loc)
        {
            double dR;
            loc.GetPoint(out dR);
            return dR;
        }

        /// <summary>
        /// 定位曲线
        /// </summary>
        /// <param name="loc"></param>
        /// <returns></returns>
        public static Curve GetCurve(this Location loc)
        {
            if (!(loc is LocationCurve)) return null;
            LocationCurve lc = loc as LocationCurve;
            return lc.Curve;
        }

        /// <summary>
        /// 定位线
        /// </summary>
        /// <param name="loc"></param>
        /// <returns></returns>
        public static Line GetLine(this Location loc)
        {
            return loc.GetCurve()?.GetLine();
        }
    }

    public static class CurveExtend
    {
        /// <summary>
        /// 起点
        /// </summary>
        /// <param name="curve"></param>
        /// <returns></returns>
        public static XYZ StartPoint(this Curve curve)
        {
            return curve.GetEndPoint(0);
        }

        /// <summary>
        /// 终点
        /// </summary>
        /// <param name="curve"></param>
        /// <returns></returns>
        public static XYZ EndPoint(this Curve curve)
        {
            return curve.GetEndPoint(1);
        }

        /// <summary>
        /// 线
        /// </summary>
        /// <param name="curve"></param>
        /// <returns></returns>
        public static Line GetLine(this Curve curve)
        {
            return curve as Line;
        }

        /// <summary>
        /// 偏移，注意圆弧只是Move效果
        /// </summary>
        public static Curve OffsetVector(this Curve curve, XYZ vec)
        {
            return curve.CreateTransformed(Transform.CreateTranslation(vec));
        }
    }

    public static class XYZExtend
    {
        /// <summary>
        /// 按逆时针排序点集(未考虑Z方向)
        /// 有些情况排序不准确，建议用SortByAnticlockwise
        /// </summary>
        /// <param name="ptList"></param>
        public static void SortCounterClockwise(this List<XYZ> ptList)
        {
            XYZ center = new XYZ();
            double num = 0.0;
            double num2 = 0.0;
            double num3 = 0.0;
            for (int i = 0; i < ptList.Count; i++)
            {
                num += ptList[i].X;
                num2 += ptList[i].Y;
                num3 += ptList[i].Z;
            }
            double x = num / (double)ptList.Count;
            double y = num2 / (double)ptList.Count;
            double z = num3 / (double)ptList.Count;
            center = new XYZ(x, y, z);
            for (int j = 0; j < ptList.Count - 1; j++)
            {
                for (int k = 0; k < ptList.Count - j - 1; k++)
                {
                    if (XYZExtend.PointCmp(ptList[k], ptList[k + 1], center))
                    {
                        XYZ value = ptList[k];
                        ptList[k] = ptList[k + 1];
                        ptList[k + 1] = value;
                    }
                }
            }
        }

        /// <summary>
        /// 若a大于点b，即点a在b顺时针方向 ，返回true，否则false
        /// 为SortCounterClockwise方法调用
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="center"></param>
        /// <returns></returns>
        private static bool PointCmp(XYZ a, XYZ b, XYZ center)
        {
            if (a.X >= 0.0 && b.X < 0.0)
            {
                return true;
            }
            if (a.X == 0.0 && b.X == 0.0)
            {
                return a.Y > b.Y;
            }
            double num = (a.X - center.X) * (b.Y - center.Y) - (b.X - center.X) * (a.Y - center.Y);
            if (num < 0.0)
            {
                return true;
            }
            if (num > 0.0)
            {
                return false;
            }
            double num2 = (a.X - center.X) * (a.X - center.X) + (a.Y - center.Y) * (a.Y - center.Y);
            double num3 = (b.X - center.X) * (b.X - center.X) + (b.Y - center.Y) * (b.Y - center.Y);
            return num2 > num3;
        }

        /// <summary>
        ///  获取闭合区域的边线
        /// </summary>
        /// <param name="ptList"></param>
        public static List<Line> GetCurveLoop(this List<XYZ> ptList)
        {
            var list = new List<Line>();
            for (int i = 0; i < ptList.Count - 1; i++)
            {
                Line item = Line.CreateBound(ptList[i], ptList[i + 1]);
                if (item == null) continue;

                list.Add(item);
            }
            Line item2 = Line.CreateBound(ptList[ptList.Count - 1], ptList[0]);
            if (item2 != null)
            {
                list.Add(item2);
            }

            return list;
        }
    }
}