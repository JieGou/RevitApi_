using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Drawing;
using Autodesk.Revit;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using System.Windows;
using goa.Common;
using g3;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.Algorithm.Hull;
using goa.Common.NtsInterOp;
using PublicProjectMethods_;
using goa.Common.g3InterOp;
using Autodesk.Revit.DB.IFC;

namespace goa.RevitUI
{
    [TransactionAttribute(TransactionMode.Manual)]
    [RegenerationAttribute(RegenerationOption.Manual)]
    public class CMD : IExternalCommand
    {
        internal static IList<FamilyInstance> allFi;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            #region 与revit文档交互入口

            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;
            View acvtiView = doc.ActiveView;

            #endregion 与revit文档交互入口

            try
            {
                //pick module
                // allFi = BasicMethod.getFiFromSel(uidoc);
                if (allFi.Count == 0)
                {
                    TaskDialog.Show("提示", "请先选中模块内的图元");
                    return Result.Cancelled;
                }
                else
                {
                    //elemIds是选中模块内的所有图元

                    //执行对模块的复制、移动、镜像、旋转
                    //Methods.Move(doc,elemIds);
                    //Methods.Copy(doc, elemIds);
                    //Methods.MirrorPick(doc, elemIds);
                    //Methods.Rotate(doc, elemIds);
                }

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                //goa.Common.UserMessages.ShowErrorMessage(ex, null);
                return Result.Failed;
            }
        }
    }

    /// <summary>
    /// 测试获取凹包
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdConcaveHull : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiapp = commandData.Application;
            var app = uiapp.Application;
            var uidoc = uiapp.ActiveUIDocument;
            var doc = uidoc.Document;
            var sel = uidoc.Selection;
            var pickrefs = sel.PickObjects(ObjectType.Element, new ClassSelectionFilter<SpatialElement>(), "选择墙");
            const int _digits = 4;
            //换算
            var unitConversionCurves = new List<List<Line>>();
            //面积过滤
            var wallList = pickrefs.Select(x => doc.GetElement(x) as Room).Where(e => e != null && e.IsValidObject).Where(r => r.Area.FromApiToSquareM() >= 10d).ToList();
            //偏移
            //List<List<CurveLoop>> wallSectionCurveLoops = wallList.Select(w => w.GetWallBottomProfile(uiapp).Select(cl => CurveLoop.CreateViaOffset(cl, 10d.MilliMeterToFeet(), XYZ.BasisZ)).ToList()).ToList();
            //foreach (var wallSectionCurves in wallSectionCurveLoops)
            //{
            //    foreach (var sectionCurves in wallSectionCurves)
            //    {
            //        var itemConversionSection = new List<Line>();
            //        foreach (var line in sectionCurves)
            //        {
            //            var start = line.StartPoint();
            //            var end = line.EndPoint();
            //            var digitStart = new XYZ(Math.Round(start.X, _digits), Math.Round(start.Y, _digits), Math.Round(start.Z, _digits));
            //            var digitEnd = new XYZ(Math.Round(end.X, _digits), Math.Round(end.Y, _digits), Math.Round(end.Z, _digits));

            //            //XYZ conversionStart = new XYZ(start.X.FeetToMilliMeter(), start.Y.FeetToMilliMeter(), start.Z.FeetToMilliMeter());
            //            //XYZ conversionEnd = new XYZ(end.X.FeetToMilliMeter(), end.Y.FeetToMilliMeter(), end.Z.FeetToMilliMeter());
            //            var itemConversionLine = Line.CreateBound(digitStart, digitEnd);
            //            itemConversionSection.Add(itemConversionLine);
            //        }
            //        unitConversionCurves.Add(itemConversionSection);
            //    }
            //}

            List<CurveLoop> roomsCurveLoops = new List<CurveLoop>();
            foreach (var room in wallList)
            {
                var cl = room.GetBoundaryByIFC(SpatialElementBoundaryLocation.Center).FirstOrDefault();
                if (cl == null || !cl.IsValidObject) continue;

                roomsCurveLoops.Add(CurveLoop.CreateViaOffset(cl, -5d.MilliMeterToFeet(), XYZ.BasisZ));
            }

            foreach (var sectionCurves in roomsCurveLoops)
            {
                var itemConversionSection = new List<Line>();
                foreach (var line in sectionCurves)
                {
                    var start = line.StartPoint();
                    var end = line.EndPoint();
                    var digitStart = new XYZ(Math.Round(start.X, _digits), Math.Round(start.Y, _digits), Math.Round(start.Z, _digits));
                    var digitEnd = new XYZ(Math.Round(end.X, _digits), Math.Round(end.Y, _digits), Math.Round(end.Z, _digits));

                    //XYZ conversionStart = new XYZ(start.X.FeetToMilliMeter(), start.Y.FeetToMilliMeter(), start.Z.FeetToMilliMeter());
                    //XYZ conversionEnd = new XYZ(end.X.FeetToMilliMeter(), end.Y.FeetToMilliMeter(), end.Z.FeetToMilliMeter());
                    var itemConversionLine = Line.CreateBound(digitStart, digitEnd);
                    itemConversionSection.Add(itemConversionLine);
                }
                unitConversionCurves.Add(itemConversionSection);
            }

            var polygons = unitConversionCurves.Select(curves => curves.ToPolygon2d().ToPolygon()).ToList();

            var gs = NtsGeometryServices.Instance;
            var gf = gs.CreateGeometryFactory();
            MultiPoint multiPointGeometry = null/*gf.CreateMultiPoint(GeometryFactory.ToPointArray(ntsPointGeometries))*/;
            //Note 单纯使用点集会出现很多预料之外的结果
            //var concaveHull = ConcaveHull.ConcaveHullByLengthRatio(multiPointGeometry, 0.1);
            //TODO 尝试先对多边形进行合并，之后再获取凹包
            MultiPolygon multiPolygon = gf.CreateMultiPolygon(polygons.ToArray());
            //获取凹包
            //TODO 解决 Unable to find a convex corner 错误
            //var concaveHull = ConcaveHullOfPolygons.ConcaveHullByLength(multiPolygon, 1d/*, true, false*/);
            var concaveHull = ConcaveHullOfPolygons.ConcaveHullByLengthRatio(multiPolygon, 0.1);
            List<Line> lines = concaveHull.ToLines().ToList();

            using (Transaction trans = new Transaction(doc, "绘制凹轮廓线"))
            {
                trans.Start();

                foreach (var line in lines)
                {
                    try
                    {
                        var detailLine = doc.Create.NewDetailCurve(doc.ActiveView, line);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }

                trans.Commit();
            }
            return Result.Succeeded;
        }
    }

    /// <summary>
    /// 测试获取obb
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdObb : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiapp = commandData.Application;
            var app = uiapp.Application;
            var uidoc = uiapp.ActiveUIDocument;
            var doc = uidoc.Document;
            var sel = uidoc.Selection;
            var pickrefs = sel.PickObjects(ObjectType.Element, new WallSelectionFilter(), "选择墙");

            var wallList = pickrefs.Select(x => doc.GetElement(x) as Wall).ToList();
            var wallSectionCurves = wallList.Select(w => w.GetPlaneBorders()).ToList();

            var polygons = wallSectionCurves.Select(curves => curves.ToPolygon2d().ToPolygon()).ToList();

            var gs = NtsGeometryServices.Instance;
            var gf = gs.CreateGeometryFactory();

            MultiPolygon multiPolygon = gf.CreateMultiPolygon(polygons.ToArray()); ;
            //TODO 改造为凸包 一堆碎线得到凸包
            var concaveHull = ConcaveHullOfPolygons.ConcaveHullByLength(multiPolygon, 1d/*, true, false*/);
            List<Line> lines = concaveHull.ToLines().ToList();
            var obb = lines.GetOrientedBoundingBox().ToList();

            using (Transaction trans = new Transaction(doc, "debug"))
            {
                trans.Start();

                foreach (var line in obb)
                {
                    try
                    {
                        var detailLine = doc.Create.NewDetailCurve(doc.ActiveView, line);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }

                trans.Commit();
            }
            return Result.Succeeded;
        }
    }

    public static class RevitRoomExtension
    {
        /// <summary>
        /// 房间轮廓+洞口 可能为null
        /// </summary>
        /// <param name="room"></param>
        /// <param name="boundaryLocation">房间边界计算选项 默认Center</param>
        /// <remarks>
        /// 边界计算选项优先设置为Center 如果为 Finish 可能不闭合
        /// </remarks>
        /// <returns></returns>
        public static List<CurveLoop> GetBoundaryByIFC(this Room room, SpatialElementBoundaryLocation boundaryLocation = SpatialElementBoundaryLocation.Center)
        {
            var options = new SpatialElementBoundaryOptions() { SpatialElementBoundaryLocation = boundaryLocation };
            try
            {
                var roomBoundary = ExporterIFCUtils.GetRoomBoundaryAsCurveLoopArray(room, options, false).ToList();

                roomBoundary.Sort(delegate (CurveLoop a, CurveLoop b)
                {
                    return ExporterIFCUtils.ComputeAreaOfCurveLoops(new List<CurveLoop> { b })
                       .CompareTo(ExporterIFCUtils.ComputeAreaOfCurveLoops(new List<CurveLoop> { a }));
                });

                return roomBoundary;
            }
            catch (System.Exception ex)
            {
                return new List<CurveLoop>();
            }
        }
    }
}