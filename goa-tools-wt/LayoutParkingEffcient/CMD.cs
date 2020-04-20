﻿using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.Attributes;

using goa.Common;
using System.Collections.ObjectModel;

namespace LayoutParkingEffcient
{
    [TransactionAttribute(TransactionMode.Manual)]
    [RegenerationAttribute(RegenerationOption.Manual)]
    public class CMD : IExternalCommand
    {
        //声明全局静态变量
        public static ObservableCollection<string> TestList = new ObservableCollection<string>();

        public static double parkingPlaceHeight = 5100;
        public static double parkingPlaceWight = 2400;
        public static double Wd = 5500;
        public static double columnWidth = 600;
        public static double Wd_main = 6000;
        public static double redline_offset_distance = 5000;

        public static string layoutMethod = "";

        public Result Execute(ExternalCommandData commandData,
                      ref string message,
                      ElementSet elements)
        {
            try
            {
                MainWindow form = APP.MainWindow;
                if (null != form && form.IsVisible == true)
                {
                    form.Activate();
                    return Result.Succeeded;
                }

                //show new window
                if (null == form || !form.IsLoaded)
                    form = new MainWindow();
                APP.MainWindow = form;
                form.Show();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }// excute
    }//class
}//namespace
