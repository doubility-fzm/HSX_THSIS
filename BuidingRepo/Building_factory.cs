using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Xbim.Common;
using Xbim.Common.Geometry;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.GeometricConstraintResource;
using Xbim.Ifc4.GeometricModelResource;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.Kernel;
using Xbim.Ifc4.MaterialResource;
using Xbim.Ifc4.MeasureResource;
using Xbim.Ifc4.PresentationAppearanceResource;
using Xbim.Ifc4.ProductExtension;
using Xbim.Ifc4.ProfileResource;
using Xbim.Ifc4.PropertyResource;
using Xbim.Ifc4.RepresentationResource;
using Xbim.Ifc4.SharedBldgElements;
using Xbim.Ifc4.SharedComponentElements;
using Xbim.IO;

namespace BuildingRepo
{
    public class Building_factory:IDisposable
    {
        private readonly string _outputPath = "";
        private readonly string _projectName = "xx工程";
        private readonly string _buildingName = "xx楼板";

        private readonly IfcStore _model;//using external Alignment model as refenrence

        private IfcStore CreateAndInitModel(string projectname)
        {
            //first we need register essential information for the project
            var credentials = new XbimEditorCredentials
            {
                ApplicationDevelopersName = "hsx",
                ApplicationFullName = "IFC Model_Alignment for Building",
                ApplicationIdentifier = "",
                ApplicationVersion = "1.0",
                EditorsFamilyName = "HE",
                EditorsGivenName = "Shixin",
                EditorsOrganisationName = "TJU"
            };
            //create model by using method in IfcStore class,using memory mode,and IFC4x1 format
            var model = IfcStore.Create(credentials, XbimSchemaVersion.Ifc4x1, XbimStoreType.InMemoryModel);
                                                                                                                                                                                                             
            //begin a transition when do any change in a model
            using (var txn = model.BeginTransaction("Initialise Model"))
            {
                //add new IfcProject item to a certain container
                var project = model.Instances.New<IfcProject>
                    (p =>
                    {
                        //Set the units to SI (mm and metres)                      
                        p.Initialize(ProjectUnits.SIUnitsUK);
                        p.Name = projectname;
                    });
                // Now commit the changes, else they will be rolled back 
                // at the end of the scope of the using statement
                txn.Commit();
            }
            return model;
        }

        public Building_factory(string outputPath= "../../TestFiles/girder.ifc")
        {
            _model = CreateAndInitModel(_projectName);
            InitWCS();           
            _outputPath = outputPath;
        }

        private IfcCartesianPoint Origin3D { get; set; }
        private IfcDirection AxisX3D { get; set; }
        private IfcDirection AxisY3D { get; set; }
        private IfcDirection AxisZ3D { get; set; }
        private IfcAxis2Placement3D WCS { get; set; }
        private IfcCartesianPoint Origin2D { get; set; }
        private IfcDirection AxisX2D { get; set; }
        private IfcDirection AxisY2D { get; set; }
        private IfcAxis2Placement2D WCS2D { get; set; }
        private void InitWCS()
        {
            using (var txn = this._model.BeginTransaction("Initialise WCS"))
            {
                var context3D = this._model.Instances.OfType<IfcGeometricRepresentationContext>()
                .Where(c => c.CoordinateSpaceDimension == 3)
                .FirstOrDefault();
                if (context3D.WorldCoordinateSystem is IfcAxis2Placement3D wcs)
                {
                    WCS = wcs;
                    Origin3D = wcs.Location;
                    AxisZ3D = toolkit_factory.MakeDirection(_model, 0, 0, 1);
                    wcs.Axis = AxisZ3D;
                    AxisX3D = toolkit_factory.MakeDirection(_model, 1, 0, 0);
                    wcs.RefDirection = AxisX3D;
                    AxisY3D = toolkit_factory.MakeDirection(_model, 0, 1, 0);
                }

                var context2D = this._model.Instances.OfType<IfcGeometricRepresentationContext>()
                    .Where(c => c.CoordinateSpaceDimension == 2)
                    .FirstOrDefault();
                if (context2D.WorldCoordinateSystem is IfcAxis2Placement2D wcs2d)
                {
                    WCS2D = wcs2d;
                    Origin2D = wcs2d.Location;
                    AxisX2D = toolkit_factory.MakeDirection(_model, 1, 0);
                    wcs2d.RefDirection = AxisX2D;
                    AxisY2D = toolkit_factory.MakeDirection(_model, 0, 1);
                }

                txn.Commit();
            }
        }
        public void Dispose()
        {
            _model.Dispose();
        }

        //_placementMap 用于记录轴网的间距、跨度以及层高
        private Dictionary<int, (List<double> spacing, List<double> span, double height)> _placementMap = new Dictionary<int, (List<double> spacing, List<double> span, double height)>();



        //GeneratePlacementMap函数：将TEST中输入的轴网层高等信息解析到类Building的成员_placementMap中保存。
        public void GeneratePlacementMap(List<List<double>> Column_spacing, List<List<double>> Column_span, List<double> height)
        {
            if ((Column_spacing.Count != Column_span.Count)||(Column_spacing.Count!=height.Count)||(Column_span.Count!=height.Count))
                throw new InvalidOperationException("Must Pair the spacing ,span and height!");
                                       //这里还要判断那个高度是不是匹配
            for (int i = 0; i < Column_spacing.Count; i++)
            {
                _placementMap[i] = (Column_spacing[i], Column_span[i], height[i]);
            }
        }


        //plate code
        #region
        public void PlateBuild() 
        {
            //写创建过程
            var site = toolkit_factory.CreateSite(_model, "Structure Site");
            double thickness = 200;
            #region
            //#region
            //var Plate = new List<IfcPlate>();
            //int count = _placementMap.Count;
            //double z = _placementMap[0].height;
            //for (int i = 0; i < count; i++)
            //{
            //    double y = 0;
            //    for (int j = 0; j < count; j++)
            //    {
            //        double x = 0;
            //        (double, double, double) startPoint = (x, y, z);
            //        (double, double, double) endPoint = (x, y + _placementMap[i].span[j], z);
            //        for (int k = 0; k < count; k++)
            //        {

            //            (double, double, double, double, double, double) plateProfile = (x, y, z, x + _placementMap[i].spacing[k], y, z);
            //            Plate.Add(CreatePlate(startPoint, endPoint, plateProfile, thickness));
            //            x += _placementMap[i].spacing[k];
            //        }
            //        y += _placementMap[i].span[j];
            //    }
            //    if (i + 1 < count)
            //        z += _placementMap[i + 1].height;
            //}
            //for (int i = 0; i < Plate.Count; i++)
            //{
            //    toolkit_factory.AddPrductIntoSpatial(_model, site, Plate[i], "Add plate to site");
            //}
            //#endregion

            #endregion
            //test1
            //(double, double, double) profile_p1 = (0, 0, 3000);
            //(double, double, double) profile_p2 = (0, 7200, 3000);
            //(double, double, double, double, double, double) plate_profile = (0,0, 3000,  8000,0, 3000);
            //var plate1 = CreatePlate(profile_p1, profile_p2, plate_profile, thickness);
            //toolkit_factory.AddPrductIntoSpatial(_model, site, plate1, "Add plate to siteata");

            //plate_profile = (8000, 0, 3000, 16000, 0, 3000);
            //var plate3 = CreatePlate(profile_p1, profile_p2, plate_profile, thickness);
            //toolkit_factory.AddPrductIntoSpatial(_model, site, plate3, "Add plate to siteata");




            (double, double, double) profile_p3 = (0, 7200, 3000);
            (double, double, double) profile_p4= (0, 14400, 3000);
            (double, double, double, double, double, double) plate_profile1 = (0, 0, 0, 8000, 0, 0);
            var plate2 = CreatePlate(profile_p3, profile_p4, plate_profile1, thickness);
            toolkit_factory.AddPrductIntoSpatial(_model, site, plate2, "Add plate to siteata");

            ////failure test
            plate_profile1 = (0, -3600, 0, 8000, -3600, 0);
            var plate5 = CreatePlate(profile_p3, profile_p4, plate_profile1, thickness);
            toolkit_factory.AddPrductIntoSpatial(_model, site, plate5, "Add plate to siteata");





            //plate_profile1 = (8000, 0, 3000, 16000, 0, 3000);
            //var plate4 = CreatePlate(profile_p3, profile_p4, plate_profile1, thickness);
            //toolkit_factory.AddPrductIntoSpatial(_model, site, plate4, "Add plate to siteata");

            //end test





            _model.SaveAs(_outputPath, StorageType.Ifc);
        }

        //绿色的板
        private IfcPlate CreatePlate((double x, double y, double z)startPoint, (double x, double y, double z) endPoint,
            (double x1, double y1, double z1, double x2, double y2, double z2) LineProfile,double thickness)
        {
            using (var txn = this._model.BeginTransaction("CreatePlate"))
            {
                var plate = this._model.Instances.New<IfcPlate>();
                plate.Name = "testPlate";
                plate.ObjectType = "Single_Plate";

                var point1 = toolkit_factory.MakeCartesianPoint(_model, startPoint.x, startPoint.y, startPoint.z);
                var point2 = toolkit_factory.MakeCartesianPoint(_model, endPoint.x, endPoint.y, endPoint.z);

                var profile_point1 = toolkit_factory.MakeCartesianPoint(_model, LineProfile.x1, LineProfile.y1, LineProfile.z1);
                var profile_point2 = toolkit_factory.MakeCartesianPoint(_model, LineProfile.x2, LineProfile.y2, LineProfile.z2);
                var profile = toolkit_factory.MakeCenterLineProfile(_model, profile_point1, profile_point2, thickness);
        
                var solid = this._model.Instances.New<IfcExtrudedAreaSolid>(); //extruded area solid:拉伸区域实体。
                                                                //有四个重要参数：SweptArea、ExtrudedDirection、Position、Depth
                solid.SweptArea = profile;
                solid.ExtrudedDirection = toolkit_factory.MakeDirection(_model, 0,0,1);   //拉伸方向为z轴
                var solid_direction=toolkit_factory.MakeDirection(_model, point1, point2);
                solid.Position = toolkit_factory.MakeLocalAxisPlacement(_model, point1, solid_direction);
                solid.Depth = toolkit_factory.GetLength(point1, point2);

                toolkit_factory.SetSurfaceColor(_model, solid, 124.0 / 255.0, 200.0 / 255.0, 49.0 / 255.0, 0.15);
                var shape = toolkit_factory.MakeShapeRepresentation(_model, 3, "Body", "AdvancedSweptSolid", solid);

                plate.Representation = this._model.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
                plate.PredefinedType = IfcPlateTypeEnum.USERDEFINED;

                txn.Commit();
                return plate;
            }
        }

        public void SlabTest()
        {
            var site = toolkit_factory.CreateSite(_model, "Structure Site");
            double thickness = 200;
            var PointSet = new List<IfcCartesianPoint>();

            using (var txn = this._model.BeginTransaction("Add Placement Points"))
            {
                var point1 = toolkit_factory.MakeCartesianPoint(_model, 0, 0, 0);
                var point2 = toolkit_factory.MakeCartesianPoint(_model, 600, 0, 0);
                var point3 = toolkit_factory.MakeCartesianPoint(_model, 600, 800, 0);
                var point4 = toolkit_factory.MakeCartesianPoint(_model, 0, 800, 0);

                PointSet.Add(point1);
                PointSet.Add(point2);
                PointSet.Add(point3);
                PointSet.Add(point4);
                PointSet.Add(point1);
                txn.Commit();
            }
            var slab1 = CreateSlab(PointSet, thickness);
            toolkit_factory.AddPrductIntoSpatial(_model, site, slab1, "Add slab to site");

            PointSet.Clear();
            using (var txn = this._model.BeginTransaction("Add Placement Points"))
            {
                var point1 = toolkit_factory.MakeCartesianPoint(_model, 700, 0, 0);
                var point2 = toolkit_factory.MakeCartesianPoint(_model, 1200, 0, 0);
                var point3 = toolkit_factory.MakeCartesianPoint(_model, 1200, 500, 0);
                var point4 = toolkit_factory.MakeCartesianPoint(_model, 1700, 500, 0);
                var point5 = toolkit_factory.MakeCartesianPoint(_model, 1700, 1000, 0);
                var point6 = toolkit_factory.MakeCartesianPoint(_model, 700, 1000, 0);

                PointSet.Add(point1);
                PointSet.Add(point2);
                PointSet.Add(point3);
                PointSet.Add(point4);
                PointSet.Add(point5);
                PointSet.Add(point6);
                PointSet.Add(point1);
                txn.Commit();
            }
            var slab2 = CreateSlab(PointSet, thickness);
            toolkit_factory.AddPrductIntoSpatial(_model, site, slab2, "Add slab to site");

            _model.SaveAs(_outputPath, StorageType.Ifc);
        }

        private IfcSlab CreateSlab(List<IfcCartesianPoint> slabProf,double thickness)
        {
            using (var txn = this._model.BeginTransaction("CreateSlab"))
            {
                var slab = this._model.Instances.New<IfcSlab>();
                slab.Name = "levelSlab";
                slab.ObjectType = "Single_Slab";

                var solid = this._model.Instances.New<IfcExtrudedAreaSolid>();
                var profile = toolkit_factory.MakeArbitraryProfile(_model, slabProf);
                solid.SweptArea = profile;
                solid.ExtrudedDirection = toolkit_factory.MakeDirection(_model, 0, 0, 1);
                //这里如果每一个楼层需要创建楼板则需要创建局部坐标，现在为了测试，只用全局坐标
                solid.Depth = thickness;

                toolkit_factory.SetSurfaceColor(_model, solid, 124.0 / 255.0, 200.0 / 255.0, 49.0 / 255.0, 0.15);
                var shape = toolkit_factory.MakeShapeRepresentation(_model, 3, "Body", "AdvancedSweptSolid", solid);

                slab.Representation = this._model.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
                slab.PredefinedType = IfcSlabTypeEnum.USERDEFINED;

                txn.Commit();
                return slab;

            }
        }

        #endregion



        //beam code
        #region
        public void BeamBuild()
        {
            var site = toolkit_factory.CreateSite(_model, "Structure Site");

            //300*600的梁
            //建立梁所需参数：形心点、拉伸方向上的一点、梁截面参数（梁宽w，梁高h)
            double width = 300;
            double height = 600;

            //建主梁
            var Mainbeam = new List<IfcBeam>();
            int count = _placementMap.Count;
            double z = _placementMap[0].height;
            for (int k = 0; k < count; k++)
            {
                double y = 0;
                for (int j = 0; j <= count; j++)
                {
                    double x = 0;
                    for (int i = 0; i < count; i++)
                    {
                        (double, double, double) shape_heart = (x, y, z);
                        (double, double, double) extruded_point = (x + _placementMap[k].spacing[i], y, z);

                        Mainbeam.Add(CreateBeam(shape_heart, extruded_point, width, height));
                        x += _placementMap[k].spacing[i];
                    }
                    if (j != count)
                        y += _placementMap[k].span[j];
                }
                if (k + 1 < count)
                    z += _placementMap[k + 1].height;
            }
            for (int i = 0; i < Mainbeam.Count; i++)
            {
                toolkit_factory.AddPrductIntoSpatial(_model, site, Mainbeam[i], "Add plate to site");
            }

            //次梁
            var Secbeam = new List<IfcBeam>();
            z = _placementMap[0].height;
            for (int k = 0; k < count; k++)
            {
                double x = 0;
                for (int j = 0; j <= count; j++)
                {
                    double y = 0;
                    for (int i = 0; i < count; i++)
                    {
                        (double, double, double) shape_heart = (x, y, z);
                        (double, double, double) extruded_point = (x, y + _placementMap[k].span[i], z);
                        Secbeam.Add(CreateSecBeam(shape_heart, extruded_point, width, height));
                        y += _placementMap[k].span[i];
                    }
                    if (j != count)
                        x += _placementMap[k].spacing[j];
                }
                if (k + 1 < count)
                    z += _placementMap[k + 1].height;
            }
            for (int i = 0; i < Secbeam.Count; i++)
            {
                toolkit_factory.AddPrductIntoSpatial(_model, site, Secbeam[i], "Add plate to site");
            }

            _model.SaveAs(_outputPath, StorageType.Ifc);
        }

        //咖啡色主梁
        private IfcBeam CreateBeam((double x, double y, double z) shape_heart, (double x, double y, double z) extruded_point,double width, double height)
        {
            using (var txn = this._model.BeginTransaction("CreateBeam"))
            {
                var beam = this._model.Instances.New<IfcBeam>();
                beam.Name = "testBeam";
                beam.ObjectType = "Single_Beam";

                var point1 = toolkit_factory.MakeCartesianPoint(_model, shape_heart);
                var point2 = toolkit_factory.MakeCartesianPoint(_model, extruded_point);

                var profile_point1 = toolkit_factory.MakeCartesianPoint(_model, 0-width/2, 0, 0);
                var profile_point2 = toolkit_factory.MakeCartesianPoint(_model, width/2, 0, 0);
                var profile = toolkit_factory.MakeCenterLineProfile(_model, profile_point1, profile_point2, height);

                var solid = this._model.Instances.New<IfcExtrudedAreaSolid>();
                solid.SweptArea = profile;
                solid.ExtrudedDirection = toolkit_factory.MakeDirection(_model, 0, 0, 1);
                var solid_direction = toolkit_factory.MakeDirection(_model, point1, point2);
                solid.Position = toolkit_factory.MakeLocalAxisPlacement(_model, point1, solid_direction);
                solid.Depth = toolkit_factory.GetLength(point1, point2);

                toolkit_factory.SetSurfaceColor(_model, solid, 124.0 / 255.0, 51.0 / 255.0, 49.0 / 255.0, 0.15);

               // toolkit_factory.SetSurfaceColor(_model, solid, 200.0 / 255.0, 20.0 / 255.0, 10.0 / 255.0, 0.15);
               //用上面语句渲染成红色

                var shape = toolkit_factory.MakeShapeRepresentation(_model, 3, "Body", "AdvancedSweptSolid", solid);

                beam.Representation = this._model.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
                beam.PredefinedType = IfcBeamTypeEnum.USERDEFINED;
                                
                txn.Commit();
                return beam;
            }
        }


        //红色次梁
        private IfcBeam CreateSecBeam((double x, double y, double z) shape_heart, (double x, double y, double z) extruded_point, double width, double height)
        {
            using (var txn = this._model.BeginTransaction("CreateBeam"))
            {
                var beam = this._model.Instances.New<IfcBeam>();
                beam.Name = "testBeam";
                beam.ObjectType = "Single_Beam";

                var point1 = toolkit_factory.MakeCartesianPoint(_model, shape_heart);
                var point2 = toolkit_factory.MakeCartesianPoint(_model, extruded_point);

                var profile_point1 = toolkit_factory.MakeCartesianPoint(_model, 0 - width / 2, 0, 0);
                var profile_point2 = toolkit_factory.MakeCartesianPoint(_model, width / 2, 0, 0);
                var profile = toolkit_factory.MakeCenterLineProfile(_model, profile_point1, profile_point2, height);

                var solid = this._model.Instances.New<IfcExtrudedAreaSolid>();
                solid.SweptArea = profile;
                solid.ExtrudedDirection = toolkit_factory.MakeDirection(_model, 0, 0, 1);
                var solid_direction = toolkit_factory.MakeDirection(_model, point1, point2);
                solid.Position = toolkit_factory.MakeLocalAxisPlacement(_model, point1, solid_direction);
                solid.Depth = toolkit_factory.GetLength(point1, point2);

                toolkit_factory.SetSurfaceColor(_model, solid, 150.0 / 255.0, 10.0 / 255.0, 10.0 / 255.0, 0.15);

                // toolkit_factory.SetSurfaceColor(_model, solid, 200.0 / 255.0, 20.0 / 255.0, 10.0 / 255.0, 0.15);
                //用上面语句渲染成红色

                var shape = toolkit_factory.MakeShapeRepresentation(_model, 3, "Body", "AdvancedSweptSolid", solid);

                beam.Representation = this._model.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
                beam.PredefinedType = IfcBeamTypeEnum.USERDEFINED;

                txn.Commit();
                return beam;
            }

        }

        #endregion


        #region
        //ParsePlacementMap函数：利用placemetMap中的建筑信息生成柱网。
        //public List<List<(IfcCartesianPoint, double height)>> ParsePlacementMap(Dictionary<int, (List<double> spancing, List<double> span, double height)> placementMap)
        //{
        //    var placementSet = new List<List<(IfcCartesianPoint, double height)>>();
        //    double x = 0;
        //    double y = 0;
        //    for (int i = 0; i < placementMap.Count; i++)
        //    {
        //        var singlePlacementSet = new List<(IfcCartesianPoint, double height)>();
        //        for (int j = 0; j < placementMap[i].spancing.Count; j++)
        //        {
        //            x = x + placementMap[i].spancing[j];
        //            for (int k = 0; k < placementMap[i].span.Count; k++)
        //            {
        //                y = y + placementMap[i].span[k];
        //                using (var txn = this._model.BeginTransaction("Generate Placemment Point"))
        //                {
        //                    var point = toolkit_factory.MakeCartesianPoint(_model, x, y, 0);
        //                    singlePlacementSet.Add((point, placementMap[i].height));
        //                    txn.Commit();
        //                }
        //            }
        //        }
        //        placementSet.Add(singlePlacementSet);
        //    }
        //    return placementSet;
        //}
        #endregion

        //column code
        #region
        public void ColumnBuild()
        {
            var site = toolkit_factory.CreateSite(_model, "Structrue Site");
            (double, double, double, double, double, double) column_profile = (-200, 0, 0, 200, 0, 0);
            var Ccolumn = new List<IfcColumn>();
            int count = _placementMap.Count;
             
            double z = 0;
            for (int k = 0; k<count;k++)
            {
                double y = 0;
                for (int j = 0; j <= count; j++)
                {
                    double x = 0;
                    for (int i = 0; i <= count; i++)
                    {
                        (double, double, double) startPoint = (x, y, z);
                        (double, double, double) endPoint = (x, y, z + _placementMap[k].height);                      
                        Ccolumn.Add(CreateColumn(startPoint, endPoint, column_profile));
                        if (i != count)
                            x += _placementMap[k].spacing[i];
                    }
                    if(j!= count)
                        y += _placementMap[k].span[j];
                }
                z +=_placementMap[k].height;
            }

            for(int i =0;i<Ccolumn.Count;i++)
            {
                toolkit_factory.AddPrductIntoSpatial(_model, site, Ccolumn[i], "Add column to site");
            }

            _model.SaveAs(_outputPath, StorageType.Ifc);

        }


    
        private IfcColumn CreateColumn((double x, double y, double z) startPoint, (double x, double y, double z) endPoint,
            (double x1, double y1, double z1, double x2, double y2, double z2) LineProfile)
        {
            using (var txn = this._model.BeginTransaction("CreateColumn"))
            {
                var column = this._model.Instances.New<IfcColumn>();
                column.Name = "testColumn";
                column.ObjectType = "Single_Column";

                var point1 = toolkit_factory.MakeCartesianPoint(_model, startPoint.x, startPoint.y,startPoint.z);
                var point2 = toolkit_factory.MakeCartesianPoint(_model, endPoint.x, endPoint.y, endPoint.z);

                var profile_point1 = toolkit_factory.MakeCartesianPoint(_model, LineProfile.x1, LineProfile.y1, LineProfile.z1);
                var profile_point2 = toolkit_factory.MakeCartesianPoint(_model, LineProfile.x2, LineProfile.y2, LineProfile.z2);
                var profile = toolkit_factory.MakeCenterLineProfile(_model, profile_point1, profile_point2, 400);


                var solid = this._model.Instances.New<IfcExtrudedAreaSolid>();
                solid.SweptArea = profile;
                solid.ExtrudedDirection = toolkit_factory.MakeDirection(_model, 0, 0, 1);
                var solid_direction = toolkit_factory.MakeDirection(_model, point1, point2);
                solid.Position = toolkit_factory.MakeLocalAxisPlacement(_model, point1, solid_direction);
                solid.Depth = toolkit_factory.GetLength(point1, point2);


                toolkit_factory.SetSurfaceColor(_model, solid, 124.0 / 255.0, 51.0 / 255.0, 49.0 / 255.0, 0.15);
                var shape = toolkit_factory.MakeShapeRepresentation(_model, 3, "Body", "AdvancedSweptSolid", solid);

                column.Representation = this._model.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
                column.PredefinedType = IfcColumnTypeEnum.USERDEFINED;


                txn.Commit();
                return column;

            }

        }
        #endregion
    }
}
