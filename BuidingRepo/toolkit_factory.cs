using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xbim.Common.Geometry;
using Xbim.Ifc;
using Xbim.Ifc4.GeometricConstraintResource;
using Xbim.Ifc4.GeometricModelResource;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.Kernel;
using Xbim.Ifc4.MeasureResource;
using Xbim.Ifc4.PresentationAppearanceResource;
using Xbim.Ifc4.ProductExtension;
using Xbim.Ifc4.ProfileResource;
using Xbim.Ifc4.PropertyResource;
using Xbim.Ifc4.RepresentationResource;


namespace BuildingRepo
{
    class toolkit_factory
    {
        public static IfcDirection MakeDirection(IfcStore m, double x = 0, double y = 0, double z = 0)
        {
            return m.Instances.New<IfcDirection>(d => d.SetXYZ(x, y, z));
        }

        public static IfcDirection MakeDirection(IfcStore m, IfcCartesianPoint p1, IfcCartesianPoint p2)
        {
            return m.Instances.New<IfcDirection>(d => d.SetXYZ(p2.X - p1.X, p2.Y - p1.Y, p2.Z - p1.Z));
        }

        public static IfcSite CreateSite(IfcStore m, string name)
        {
            using (var txn = m.BeginTransaction("Create Site"))
            {
                var site = m.Instances.New<IfcSite>(s =>
                {
                    s.Name = name;
                    s.CompositionType = IfcElementCompositionEnum.ELEMENT;
                });
                var project = m.Instances.OfType<IfcProject>().FirstOrDefault();
                project.AddSite(site);
                txn.Commit();
                return site;
            }
        }

        public static void AddPrductIntoSpatial(IfcStore m, IfcSpatialStructureElement spatial, IfcProduct p, string txt)
        {
            using (var txn = m.BeginTransaction(txt))
            {
                spatial.AddElement(p);
                txn.Commit();
            }
        }

        public static IfcCartesianPoint MakeCartesianPoint(IfcStore m,double x=0,double y=0,double z=0)
        {
            return m.Instances.New<IfcCartesianPoint>(p => p.SetXYZ(x, y, z));
        }

        public static IfcCartesianPoint MakeCartesianPoint(IfcStore m, (double x,double y,double z)point)
        {
            return m.Instances.New<IfcCartesianPoint>(p => p.SetXYZ(point.x, point.y, point.z));
        }

        public static IfcAxis2Placement3D MakeLocalAxisPlacement(IfcStore m,IfcCartesianPoint originPoint,IfcDirection direction)
        {
            var origin = originPoint;
            var locZ = MakeDirection(m, 0, 0, 1);  //locZ是IfcDirection类型的
            var locX = MakeDirection(m, 1, 0, 0); //locX是IfcDirection类型的
            var dire = direction.Normalise();     //dire是 XbimVector3D类型的

            //Normalise这个函数把IfcDirection -> XbimVector3D数据转换的接口，xbim3d更底层一些
            //Normalized这函数是在xbim下的向量单位化函数
            //xbim是在几何层上进行的操作，所以可以直接拿向量的length
            //另外为什么要用EPSON自己看着办

            XbimVector3D ZNor = locZ.Normalise().Normalized();   //ZNor是XbimVector3D类型下的单位 Z向量
            var Ndire = dire.Normalized();                       //Ndire是XbimVector3D类型下的单位  输入向量

            const double EPSON = 0.1;
            var off = (Ndire - ZNor).Length;   
            if(Ndire.Z<0)    //说明Ndire是负向量
            {
                off = (Ndire + ZNor).Length;
            }
            if(off<=EPSON)  //Ndire与ZNor近似重合，局部坐标即为整体坐标。
                return MakeAxis2Placement3D(m, origin, locZ, locX);
            else
            {           
                var vz = new XbimVector3D(0, 0, 1);
                var vx = dire.CrossProduct(vz);
                locZ = MakeDirection(m, dire.X, dire.Y, dire.Z);   //将XbimVector3D类型的dire转化为IfcDirection类型，并作为局部Z轴
                locX = MakeDirection(m, vx.X, vx.Y, vx.Z);         //局部X轴为dire与Z轴叉乘后的方向；
                return MakeAxis2Placement3D(m, origin, locZ, locX);
            }
        }

        public static void SetSurfaceColor(IfcStore m, IfcGeometricRepresentationItem geoitem, 
                                            double red, double green, double blue, double transparency = 0)
        {
            var styleditem = m.Instances.New<IfcStyledItem>(si =>
            {
                si.Item = geoitem;
                si.Styles.Add(m.Instances.New<IfcSurfaceStyle>(ss =>
                {
                    ss.Side = IfcSurfaceSide.POSITIVE;
                    ss.Styles.Add(m.Instances.New<IfcSurfaceStyleRendering>(ssha =>
                    {
                        ssha.SurfaceColour = m.Instances.New<IfcColourRgb>(c =>
                        {
                            c.Red = red;
                            c.Green = green;
                            c.Blue = blue;
                        });
                        ssha.Transparency = transparency;
                    }));
                }));
            });
        }

        public static IfcShapeRepresentation MakeShapeRepresentation(IfcStore m, int dimention, string identifier, string type,
                                                IfcRepresentationItem item)
        {
            return m.Instances.New<IfcShapeRepresentation>(sr =>
            {
                sr.ContextOfItems = m.Instances.OfType<IfcGeometricRepresentationContext>()
                .Where(c => c.CoordinateSpaceDimension == 3)
                .FirstOrDefault();
                sr.RepresentationIdentifier = identifier;
                sr.RepresentationType = type;
                sr.Items.Add(item);
            });
        }

        public static IfcCenterLineProfileDef MakeCenterLineProfile(IfcStore m, IfcBoundedCurve curve, double thickness)
        {
            return m.Instances.New<IfcCenterLineProfileDef>(c =>
            {
                c.Thickness = thickness;
                c.Curve = curve;
            });
        }

        public static IfcCenterLineProfileDef MakeCenterLineProfile(IfcStore m, IfcCartesianPoint start, IfcCartesianPoint end,
                        double thickness)
        {
            var line = MakePolyLine(m, start, end);
            return MakeCenterLineProfile(m, line, thickness);
        }

        public static IfcArbitraryClosedProfileDef MakeArbitraryProfile(IfcStore m,List<IfcCartesianPoint> profilePointSet)
        {
            return m.Instances.New<IfcArbitraryClosedProfileDef>(prof =>
            {
                prof.OuterCurve = MakePolyLine(m, profilePointSet);
            });
        }

        public static IfcPolyline MakePolyLine(IfcStore m, IfcCartesianPoint start, IfcCartesianPoint end)
        {
            return MakePolyLine(m, new List<IfcCartesianPoint>() { start, end });
        }

        public static IfcPolyline MakePolyLine(IfcStore m, List<IfcCartesianPoint> points)
        {
            return m.Instances.New<IfcPolyline>(pl =>
            {
                foreach (var point in points)
                {
                    pl.Points.Add(point);
                }
            });
        }

        public static IfcAxis2Placement3D MakeAxis2Placement3D(IfcStore m, IfcCartesianPoint origin = null,
                                                    IfcDirection localZ = null, IfcDirection localX = null)
        {
            return m.Instances.New<IfcAxis2Placement3D>(a =>
            {
                a.Location = origin ?? MakeCartesianPoint(m, 0, 0, 0);
                a.Axis = localZ;
                a.RefDirection = localX;
            });
        }


        public static double GetLength(IfcCartesianPoint p1, IfcCartesianPoint p2)
        {
            return Math.Sqrt((p1.X - p2.X) * (p1.X - p2.X) + (p1.Y - p2.Y) * (p1.Y - p2.Y) + (p1.Z - p2.Z) * (p1.Z - p2.Z));
        }

    }
}
