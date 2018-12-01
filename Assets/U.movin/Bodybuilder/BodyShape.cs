﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.VectorGraphics;

namespace U.movin
{
    public class BodyShape
    {

        public GameObject gameObject;
        public Transform transform
        {
            get { return gameObject.transform; }
        }

        public BodyShapeSlave[] slaves;
        public BodymovinShape content;
        public Shape shape;
        public Scene scene;
        public Mesh mesh;
        public MeshFilter filter;
        public MeshRenderer renderer;
        public List<VectorUtils.Geometry> geoms;
        public VectorUtils.TessellationOptions options;

        public BodyPoint[] points;
        public BodyPoint[] startPoints;
        public BodyPoint[] endPoints;

        public Movin body;
        public BodyLayer layer;
        public BezierPathSegment[] segments;
        public bool closed;

        PathProperties props;
        public SolidFill fill;
        public Stroke stroke;

        public bool animated = false;
        public bool strokeColorAnimated = false;
        public bool fillColorAnimated = false;

        public MotionProps motion;
        public MotionProps mstrokec;
        public MotionProps mfillc;

        public BodymovinAnimatedShapeProperties[] motionSet;

        public BodyShape(BodyLayer layer, BodymovinShape content)
        {

            this.content = content;
            if (content.paths == null || content.paths.Length < 1) { Debug.Log("DON'T DRAW SHAPE -> NO PTS"); return; }
            
            this.layer = layer;
            this.body = layer.body;
            Transform parent = layer.transform;


            /* FIRST SHAPE PROPS */

            points = (BodyPoint[])content.paths[0].points.Clone();
            motionSet = content.paths[0].animSets;
            closed = content.paths[0].closed;



            /* ANIM SETUP */

            MotionSetup(ref animated, ref motion, motionSet);
            MotionSetup(ref strokeColorAnimated, ref mstrokec, content.strokeColorSets);
            MotionSetup(ref fillColorAnimated, ref mfillc, content.fillColorSets);



            /* GAMEOBJECT, MESH, MATERIAL */

            gameObject = new GameObject(content.item.ty + " pts: " + points.Length + "  closed: " + closed);
            transform.SetParent(parent, false);
            transform.localPosition = -layer.content.anchorPoint;

            mesh = new Mesh();
            filter = gameObject.AddComponent<MeshFilter>();
            filter.mesh = mesh;

            renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            //renderer.material = new Material(Shader.Find("Unlit/Vector"));
            //Debug.Log("sort:  " + renderer.sortingOrder);



            /* SETUP VECTOR */

            Color stClr = (content.strokeColor == null) ? new Color(1, 1, 1) : new Color(content.strokeColor[0], content.strokeColor[1], content.strokeColor[2]);
            Color flClr = (content.fillColor == null) ? new Color(1, 1, 1) : new Color(content.fillColor[0], content.fillColor[1], content.fillColor[2]);

            fill = content.fillHidden || content.fillColor == null ? null : new SolidFill() { Color = flClr };
            stroke = content.strokeHidden || content.strokeColor == null ? null : new Stroke() { Color = stClr, HalfThickness = content.strokeWidth * body.strokeScale };
            props = new PathProperties() { Stroke = stroke };

            shape = new Shape() {
                Fill = fill,
                PathProps = props,
                FillTransform = Matrix2D.identity
            };

            options = body.options;

            scene = new Scene() {
                Root = new SceneNode() { Shapes = new List<Shape> { shape } }
            };

            UpdateMesh();



            // ADDITIONAL SHAPE PATHS 

            slaves = new BodyShapeSlave[content.paths.Length - 1];
            for (int i = 1; i <= slaves.Length; i++) {
                slaves[i - 1] = new BodyShapeSlave(this, content.paths[i], body.strokeScale);
            }
            
        }

        


        public void UpdateSegments(BodyPoint[] pts, ref BezierPathSegment[] segs)
        {
            float y = -1f;
           
            for (int i = 0; i < pts.Length; i++)
            {
                BodyPoint point = pts[i];

                // Next point...

                bool last = i >= pts.Length - 1;
                BodyPoint nextPoint = last ? pts[0] : pts[i + 1];


                // UPDATE segment

                segs[i].P0.x = point.p.x;
                segs[i].P0.y = point.p.y * y;

                segs[i].P1.x = point.p.x + point.o.x;
                segs[i].P1.y = (point.p.y + point.o.y) * y;

                segs[i].P2.x = nextPoint.p.x + nextPoint.i.x;
                segs[i].P2.y = (nextPoint.p.y + nextPoint.i.y) * y;
              
            }

            int l = segs.Length - 1;
            
            segs[l].P0.x = pts[0].p.x;
            segs[l].P0.y = pts[0].p.y * y;

            segs[l].P1.x = segs[l].P1.y = segs[l].P2.x = segs[l].P2.y = 0;

        }


        public BezierPathSegment[] ConvertPointsToSegments(BodyPoint[] pts)
        {
            float y = -1f;

            int cnt = pts.Length + (closed ? 1 : 0);
            BezierPathSegment[] segs = new BezierPathSegment[cnt];

            int i = 0;
            foreach (BodyPoint point in pts)
            {

                // Next point...

                bool last = i >= pts.Length - 1;
                BodyPoint nextPoint = last ? pts[0] : pts[i + 1];


                // Make segment

                BezierPathSegment s = new BezierPathSegment()
                {
                    P0 = new Vector2(point.p.x, point.p.y * y),
                    P1 = new Vector2((point.p.x + point.o.x), (point.p.y + point.o.y) * y),
                    P2 = new Vector2((nextPoint.p.x + nextPoint.i.x), (nextPoint.p.y + nextPoint.i.y) * y)
                };

                segs[i] = s;
                i += 1;
            }

            if (pts.Length > 0 && i == cnt - 1)
            {
                BezierPathSegment final = new BezierPathSegment()
                {
                    P0 = new Vector2(pts[0].p.x, pts[0].p.y * y)
                };

                segs[i] = final;
            }


            /* READOUT */

            //foreach (BezierPathSegment s in segs)
            //{
            //    Debug.Log("P0: " + s.P0 + "  P1: " + s.P1 + "  P2: " + s.P2);
            //}

            return segs;
        }


        public void Update(float frame)
        {
            
            /* ----- ANIM PROPS ----- */

            if (animated && !motion.completed) {
                UpdateProperty(frame, ref motion, motionSet);
            }
            if (strokeColorAnimated && !mstrokec.completed) {
                UpdateProperty(frame, ref mstrokec, content.strokeColorSets);
            }
            if (fillColorAnimated && !mfillc.completed) {
                UpdateProperty(frame, ref mfillc, content.fillColorSets);
            }

            if ((animated && !motion.completed) || (strokeColorAnimated && !mstrokec.completed) || (fillColorAnimated && !mfillc.completed))
                FillMesh();
        }


        public void UpdateOpacity(float opacity)
        {
            Color c = renderer.material.color;
            c.a = opacity * 0.01f;

            renderer.material.color = c;
        }


        public void UpdateProperty(float frame, ref MotionProps m, BodymovinAnimatedShapeProperties[] set)
        {

            /* ----- CHECK FOR COMPLETE ----- */

            if (m.keys <= 0)
            {
                //Debug.Log(">>> NO PROP KEYS TO ANIMATE!");
                m.completed = true;
                return;
            }

            if (frame >= m.endFrame)
            {
                if (m.key + 1 == set.Length - 1)
                {
                    m.completed = true;
                    //Debug.Log("****** Prop Animation done! ******");
                    return;
                }

                SetKeyframe(ref m, set, m.key + 1);
            }


            /* ----- PERCENT KEYFRAME COMPLETE ----- */

            m.percent = (frame - m.startFrame) / (m.endFrame - m.startFrame);


            /* ----- CUBIC BEZIER EASE ----- */

            float ease = Ease.CubicBezier(Vector2.zero, m.currentOutTangent, m.nextInTangent, Vector2.one, m.percent);


            /* ----- UPDATE POINTS ----- */

            for (int i = 0; i < points.Length; i++)
            {
                if (m.percent < 0)
                {
                    // BACK TO START OF KEYFRAME
                    points[i].p = startPoints[i].p;
                    points[i].i = startPoints[i].i;
                    points[i].o = startPoints[i].o;
                } else
                {
                    points[i].p = startPoints[i].p + ((endPoints[i].p - startPoints[i].p) * ease);
                    points[i].i = startPoints[i].i + ((endPoints[i].i - startPoints[i].i) * ease);
                    points[i].o = startPoints[i].o + ((endPoints[i].o - startPoints[i].o) * ease);
                }
            }

            
            /* ----- UPDATE MESH ----- */

            UpdateMesh(false);

        }


        public void UpdateProperty(float frame, ref MotionProps m, BodymovinAnimatedProperties[] set)
        {

            /* ----- CHECK FOR COMPLETE ----- */

            if (m.keys <= 0)
            {
                //Debug.Log(">>> NO PROP KEYS TO ANIMATE!");
                m.completed = true;
                return;
            }

            if (frame >= m.endFrame)
            {
                if (m.key + 1 == set.Length - 1)
                {
                    m.completed = true;
                    //Debug.Log("****** Prop Animation done! ******");
                    return;
                }

                SetKeyframe(ref m, set, m.key + 1);
            }


            /* ----- PERCENT KEYFRAME COMPLETE ----- */

            m.percent = (frame - m.startFrame) / (m.endFrame - m.startFrame);


            /* ----- CUBIC BEZIER EASE ----- */

            //Debug.Log("to:  " + m.currentOutTangent + "   ti:  " + m.nextInTangent);
            
            float ease = Ease.CubicBezier(Vector2.zero, m.currentOutTangent, m.nextInTangent, Vector2.one, m.percent);


            /* ----- UPDATE PROPERTY ----- */

            if (set == content.strokeColorSets) {

                Color c = stroke.Color;
                Vector3 v = Value3(m, set, ease);
                c.r = v.x;
                c.g = v.y;
                c.b = v.z;

                stroke.Color = c;
                props.Stroke = stroke;

                if (slaves == null) { return; }
                foreach (BodyShapeSlave slave in slaves)
                {
                    slave.UpdateStrokeColor(c);
                }

            } else if (set == content.fillColorSets) {
                //Debug.Log("diff:  " + (set[m.key].e.x - set[m.key].s.x).ToString("F4") + "   fnl:  " + (set[m.key].s + ((set[m.key].e - set[m.key].s) * ease)) + "   percent:  " + m.percent + "   ease:  " + ease);

                Color c = fill.Color;
                Vector3 v = Value3(m, set, ease);
                c.r = v.x;
                c.g = v.y;
                c.b = v.z;

                fill.Color = c;
                shape.Fill = fill;

                if (slaves == null) { return; }
                foreach (BodyShapeSlave slave in slaves)
                {
                    slave.UpdateFillColor(c);
                }
            }

        }



        public Vector3 Value3(MotionProps m, BodymovinAnimatedProperties[] set, float ease)
        {
            return m.percent < 0 ?
                    set[m.key].s : set[m.key].s + ((set[m.key].e - set[m.key].s) * ease);
        }

        //public Vector3 Value3b(MotionProps m, BodymovinAnimatedProperties[] set, float ease)
        //{
        //    float x = m.percent < 0 ?
        //            set[m.key].s.x : set[m.key].s.x + ((set[m.key].e.x - set[m.key].s.x) * ease);

        //    float y = m.percent < 0 ?
        //            set[m.key].s.y : set[m.key].s.y + ((set[m.key].e.y - set[m.key].s.y) * ease);

        //    float z = m.percent < 0 ?
        //            set[m.key].s.z : set[m.key].s.z + ((set[m.key].e.z - set[m.key].s.z) * ease);

        //    return new Vector3(x, y, z);
        //}



        public void ResetKeyframes()
        {
            if (animated) { SetKeyframe(ref motion, motionSet, 0); }
            if (strokeColorAnimated) { SetKeyframe(ref mstrokec, content.strokeColorSets, 0); }
            if (fillColorAnimated) { SetKeyframe(ref mfillc, content.fillColorSets, 0); }

            if (slaves == null) { return; }
            foreach (BodyShapeSlave slave in slaves) {
                slave.ResetKeyframes();
            }
        }




        /* ----- MOTION SETUP ------ */

        public void MotionSetup(ref bool b, ref MotionProps prop, BodymovinAnimatedProperties[] set)
        {
            b = set != null && set.Length > 0;
            if (b)
            {
                prop = new MotionProps { keys = set.Length };
                SetKeyframe(ref prop, set, 0);
            }
        }

        public void MotionSetup(ref bool b, ref MotionProps prop, BodymovinAnimatedShapeProperties[] set)
        {
            b = set != null && set.Length > 0;
            if (b)
            {
                prop = new MotionProps { keys = set.Length };
                SetKeyframe(ref prop, set, 0);
            }
        }



        /* ----- KEYFRAME SETTERS ----- */

        public void SetKeyframe(ref MotionProps prop, BodymovinAnimatedProperties[] set, int k = 0)
        {
            prop.completed = false;
            if (prop.keys <= 0) { return; }

            prop.key = k;
            prop.startFrame = set[k].t;
            prop.endFrame = set.Length > k ? set[k + 1].t : prop.startFrame;
            prop.currentOutTangent = set[k].o;
            prop.nextInTangent = set[k].i;

        }

        public void SetKeyframe(ref MotionProps prop, BodymovinAnimatedShapeProperties[] set, int k = 0)
        {
            prop.completed = false;
            if (prop.keys <= 0) { return; }

            prop.key = k;
            prop.startFrame = set[k].t;
            prop.endFrame = set.Length > k ? set[k + 1].t : prop.startFrame;
            prop.currentOutTangent = set[k].o;
            prop.nextInTangent = set[k].i;

            if (set == motionSet)
            {
                startPoints = set[k].pts[0];
                endPoints = set[k].pts[1];
            }
           
        }



        /* ----- UPDATE MESH ----- */

        public void UpdateMesh(bool redraw = true)
        {
            if (segments == null) {
                segments = ConvertPointsToSegments(points);
                shape.Contours = new BezierContour[] { new BezierContour() { Segments = segments, Closed = closed } };
            } else {
                UpdateSegments(points, ref segments);
            }

            if (redraw)
                FillMesh();
        }

        public void FillMesh()
        {
            geoms = VectorUtils.TessellateScene(scene, options);
            VectorUtils.FillMesh(mesh, geoms, 1.0f);
        }

    }
}
 