using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Delaunay.Geo;
using System;
using Delaunay;

namespace SA
{
    [CreateAssetMenu]
    public class LevelGenerator : ScriptableObject
    {
        public List<GeneratorCell> cells = new List<GeneratorCell>();
        public List<LineSegment> delaunayLines = new List<LineSegment>();
        public List<LineSegment> spanningTree = new List<LineSegment>();

        public List<Path> paths = new List<Path>();

        float widthAvg = 0;
        float heightAvg = 0;

        float mainRoomMeanCutoff = 5;
        float percFromGraphToPaths = 0.1f;

        public LevelStats levelStats;

        public delegate void LeveGenerationComplete();
        public LeveGenerationComplete OnLevelGenerationComplete;

        float maxX;
        float maxY;
        float minX;
        float minY;
        public bool debug;
       
        public void GenerateLevel()
        {
            LevelGeneratorHeader header = LevelGeneratorHeader.singleton;

            if(header == null)
            {
                GameObject go = new GameObject();
                go.name = "Level Generator";
                header = go.AddComponent<LevelGeneratorHeader>();
            }

            header.StartCoroutine(Generate());
        }

        public IEnumerator Generate()
        {
            CreateCells();
            if(debug)
            {
                Debug.Log("Cells Generated");
                yield return new WaitForSeconds(1);
            }

            SeparateCells();
            if (debug)
            {
                Debug.Log("Cells Separated");
                yield return new WaitForSeconds(1);
            }

            PickMainRooms();
            if (debug)
            {
                Debug.Log("Main Rooms Picked");
                yield return new WaitForSeconds(1);
            }

            Triangulate();
            if (debug)
            {
                Debug.Log("Triangulated");
                yield return new WaitForSeconds(1);
            }


            SelectPaths();
            if (debug)
            {
                Debug.Log("Paths Selected");
                yield return new WaitForSeconds(1);
            }

//             FindCellLines();
            if (debug)
            {
                Debug.Log("FindCellLines");
                yield return new WaitForSeconds(1);
            }

            FindPathBetweenBlocks();
            if (debug)
            {
                Debug.Log("FindPathBetweenBlocks");
                yield return new WaitForSeconds(1);
            }

            FindPathRoomsBetweenMainRooms();
            if (debug)
            {
                Debug.Log("FindPathRoomsBetweenMainRooms");
                yield return new WaitForSeconds(1);
            }

            if(OnLevelGenerationComplete != null)
            {
                OnLevelGenerationComplete();
            }

            yield return null;
        }

        #region Generation Methods
        void CreateCells()
        {
            RandomFromDistribution.ConfidenceLevel_e conf_level = RandomFromDistribution.ConfidenceLevel_e._80;

            int numberOfCells = levelStats.numberOfCells;
            float roomCircleRadius = levelStats.roomCircleRadius;
            percFromGraphToPaths = levelStats.percFromGraphToPaths;
            mainRoomMeanCutoff = levelStats.mainRoomCutoff;

            float cellMinWidth = levelStats.cellMinWidth;
            float cellMaxWidth = levelStats.cellMaxWidth;
            float cellMinHeight = levelStats.cellMinHeight;
            float cellMaxHeight = levelStats.cellMaxHeight;

            for (int i = 0; i < numberOfCells; i++)
            {
                float minWidthScalar = cellMinWidth;
                float maxWidthScalar = cellMaxWidth;
                float minHeightScalar = cellMinHeight;
                float maxHeightScalar = cellMaxHeight;

                GeneratorCell cell = new GeneratorCell();
                cell.width = Mathf.RoundToInt(RandomFromDistribution.RandomRangeNormalDistribution(minWidthScalar, maxWidthScalar, conf_level));
                cell.height = Mathf.RoundToInt(RandomFromDistribution.RandomRangeNormalDistribution(minHeightScalar, maxHeightScalar, conf_level));


                Vector2 pos = GetRandomPointInCirlce(roomCircleRadius);
                cell.posX = Mathf.RoundToInt(pos.x);
                cell.posY = Mathf.RoundToInt(pos.y);
                cell.index = i;
                cells.Add(cell);
                widthAvg += cell.width;
                heightAvg += cell.height;
            }

            widthAvg /= cells.Count;
            heightAvg /= cells.Count;
        }

        void SeparateCells()
        {
            bool cellCollision = true;
            int loop = 0;
            while(cellCollision)
            {
                loop++;
                cellCollision = false;

                for (int i = 0; i < cells.Count; i++)
                {
                    GeneratorCell cell = cells[i];

					///Check all the other cells in the list if it's colliding with our cell
                    for (int j = i + 1; j < cells.Count; j++)
                    {
                        GeneratorCell cell2 = cells[j];
                        if(cell.CollidesWith(cell2))
                        {
                            cellCollision = true;

                            int cell2XOverlap = Mathf.RoundToInt((cell.posX + cell.width) - cell2.posX);
                            int cellXOverlap = Mathf.RoundToInt((cell2.posX + cell2.width) - cell.posX);

                            int cell2YOverlap = Mathf.RoundToInt((cell.posY + cell.height) - cell2.posY);
                            int cellYOverlap = Mathf.RoundToInt((cell2.posY + cell2.height) - cell.posY);
							//If the cells are colliding. Move the one on the left.
                            if (cellXOverlap < cell2XOverlap)
                            {
								//Move horizontally if distance from the origin has more bias towards Y. 
								//Otherwise, Move Vertically
                                if (cellXOverlap < cellYOverlap)
                                {
                                    cell.OffsetPosition(cellXOverlap, 0);
                                }
                                else
                                {
                                    cell.OffsetPosition(0, cellYOverlap);
                                }
                            }
                            else
                            {
                                if (cell2XOverlap < cell2YOverlap)
                                {
                                    cell2.OffsetPosition(cell2XOverlap, 0);
                                }
                                else
                                {
                                    cell2.OffsetPosition(0, cell2YOverlap);
                                }
                            }
                        }
                    }
                }
            }
        }

        void PickMainRooms()
        {
            foreach (GeneratorCell c in cells)
            {
                if(c.width * mainRoomMeanCutoff < widthAvg || c.height * mainRoomMeanCutoff < heightAvg)
                {
                    c.isMainRoom = false;
                }
            }
        }

        void Triangulate()
        {
            List<Vector2> points = new List<Vector2>();
            List<uint> colors = new List<uint>();

            Vector2 min = Vector2.positiveInfinity;
            Vector2 max = Vector2.zero;

            foreach (GeneratorCell c in  cells)
            {
                if (c.isMainRoom)
                {
                    colors.Add(0);
                    points.Add(new Vector2(c.posX + (c.width / 2), c.posY + (c.height / 2)));
                    min.x = Mathf.Min(c.posX, min.x);
                    min.y = Mathf.Min(c.posY, min.y);

                    max.x = Mathf.Max(c.posX, max.x);
                    max.y = Mathf.Max(c.posY, max.y);
                }
            }

            Voronoi v = new Voronoi(points, colors, new Rect(min.x, min.y, max.x, max.y));
            delaunayLines = v.DelaunayTriangulation();
            spanningTree = v.SpanningTree(KruskalType.MINIMUM);
        }

        void SelectPaths()
        {
            int countOfPaths = Mathf.RoundToInt(delaunayLines.Count * percFromGraphToPaths);
            int pathsAdded = 0;

            List<LineSegment> linesToAdd = new List<LineSegment>();
            for (int i = 0; i < delaunayLines.Count; i++)
            {
                if(pathsAdded >= countOfPaths)
                {
                    break;
                }

                LineSegment line = delaunayLines[i];
                bool lineExist = false;

                for (int j = 0; j < spanningTree.Count; j++)
                {
                    LineSegment spLine = spanningTree[j];
                    if(spLine.p0.Value.Equals(line.p0.Value) && spLine.p1.Value.Equals(line.p1.Value))
                    {
                        lineExist = true;
                        break;
                    }
                }

                if(!lineExist)
                {
                    linesToAdd.Add(line);
                    pathsAdded++;
                }
            }

            if (debug)
                Debug.Log("Lines to add : " + linesToAdd.Count);

            spanningTree.AddRange(linesToAdd);
            delaunayLines.Clear();
        }
        void FindPathBetweenBlocks()
        {
            foreach (LineSegment l in spanningTree)
            {
                Path path = new Path();
                path.from = GetCellByPoint(l.p0.Value.x, l.p0.Value.y);
                path.to = GetCellByPoint(l.p1.Value.x, l.p1.Value.y);

                Vector2 startPoint = l.p0.Value;
                Vector2 endPoint = l.p1.Value;

                BlockPath bl1 = new BlockPath();
                bl1.start = startPoint;
                bl1.end = new Vector2(endPoint.x, startPoint.y);

                BlockPath bl2 = new BlockPath();
                bl2.start = bl1.end;
                bl2.end = endPoint;

                path.path.Add(bl1);
                path.path.Add(bl2);
                paths.Add(path);
            }

            spanningTree.Clear();
        }

        void FindPathRoomsBetweenMainRooms()
        {
            foreach(Path p in paths)
            {
                foreach (GeneratorCell c in cells)
                {
                    if(!c.isMainRoom && !c.isPathRoom)
                    {
                        foreach (BlockPath bp in p.path)
                        {
                            if (LineRectangleInteresection(bp, c))
                            {
                                c.isPathRoom = true;
                                break;
                            }
                        }
                    }
                }
            }

            int c_index = 0;

            while(c_index < cells.Count)
            {
                GeneratorCell c = cells[c_index];
                if(c.isMainRoom || c.isPathRoom)
                {
                    maxX = Mathf.Max(c.posX + c.width, maxX);
                    maxY = Mathf.Max(c.posY + c.height, maxY);
                    minX = Mathf.Min(c.posX, minX);
                    minY = Mathf.Min(c.posY, minY);

                    c_index++;
                }
                else
                {
                    cells.Remove(c);
                }
            }

            foreach (GeneratorCell c in cells)
            {
                c.posX += Mathf.CeilToInt(Mathf.Abs(minX));
                c.posY += Mathf.CeilToInt(Mathf.Abs(minY));
                maxX = Mathf.Max(c.posX, c.width, maxX);
                maxY = Mathf.Max(c.posY + c.height, maxY);
            }

            foreach (Path p in paths)
            {
                foreach (BlockPath bp in p.path)
                {
                    bp.start.x += Mathf.Abs(minX);
                    bp.start.y += Mathf.Abs(minY);
                    bp.end.x += Mathf.Abs(minX);
                    bp.end.y += Mathf.Abs(minY);
                }
            }
        }
        #endregion

        #region Helper Methods
        Vector2 GetRandomPointInCirlce(float radius)
        {
            Vector2 retVal = Vector2.zero;

            float t = 2 * Mathf.PI * UnityEngine.Random.Range(0, 1f);
            float u = UnityEngine.Random.Range(0, 1f) + UnityEngine.Random.Range(0, 1f);

            float r = 0;

            if (u > 1)
            {
                r = 2 - u;
            }
            else
            {
                r = u;
            }

            retVal.x = radius * r * Mathf.Cos(t);
            retVal.y = radius * r * Mathf.Sin(t);

            return retVal;
        }

		/// <summary>
		/// Finds a cell at the given point.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
        GeneratorCell GetCellByPoint(float x, float y)
        {
            GeneratorCell retCell = null;
            foreach (GeneratorCell c in cells)
            {
                if (c.posX < x && c.posY < y && c.posX + c.width > x && c.posY + c.height > y)
                {
                    retCell = c;
                    break;
                }
            }

            return retCell;
        }

        //implementation by https://stackoverflow.com/a/3746601
        bool LineIntersects(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2, out Vector2 intersection)
        {
            intersection = Vector2.zero;

            Vector2 b = a2 - a1;
            Vector2 d = b2 - b1;
            float bDotDPerp = b.x * b.y - b.y * d.x;

            // if b dot d == 0, it means the lines are parallel so have infinite intersection points
            if (bDotDPerp == 0)
                return false;

            Vector2 c = b1 - a1;
            float t = (c.x * d.y - c.y * d.x) / bDotDPerp;
            if (t < 0 || t > 1)
                return false;

            float u = (c.x * b.y - c.y * b.x) / bDotDPerp;
            if (u < 0 || u > 1)
                return false;

            intersection = a1 + t * b;
            return true;
        }

        bool LineRectangleInteresection(BlockPath line, GeneratorCell rect)
        {
            bool retVal = false;
            Vector2 intersection;

            BlockPath topLine = new BlockPath();
            topLine.start = new Vector2(rect.posX, rect.posY + rect.height);
            topLine.end = new Vector2(rect.posX + rect.width, rect.posY + rect.height);
            if (LineIntersects(line.start, line.end, topLine.start, topLine.end, out intersection))
            {
                retVal = true;
            }

            BlockPath rightLine = new BlockPath();
            rightLine.start = new Vector2(rect.posX + rect.width, rect.posY + rect.height);
            rightLine.end = new Vector2(rect.posX + rect.width, rect.posY);
            if (LineIntersects(line.start, line.end, rightLine.start, rightLine.end, out intersection))
            {
                retVal = true;
            }


            BlockPath bottomLine = new BlockPath();
            bottomLine.start = new Vector2(rect.posX, rect.posY);
            bottomLine.end = new Vector2(rect.posX + rect.width, rect.posY);
            if (LineIntersects(line.start, line.end, bottomLine.start, bottomLine.end, out intersection))
            {
                retVal = true;
            }


            BlockPath leftLine = new BlockPath();
            leftLine.start = new Vector2(rect.posX, rect.posY);
            leftLine.end = new Vector2(rect.posX, rect.posY + rect.height);
            if (LineIntersects(line.start, line.end, leftLine.start, leftLine.end, out intersection))
            {
                retVal = true;
            }

            return retVal;
        }

        public Vector2 GetMaxXY()
        {
            return new Vector2(maxX, maxY);
        }

        public Vector2 GetMinXY()
        {
            return new Vector2(minX, minY);
        }

        #endregion
        }

    public class Path
    {
        public GeneratorCell from;
        public GeneratorCell to;
        public List<BlockPath> path = new List<BlockPath>();
    }

    public class BlockPath
    {
        public Vector2 start;
        public Vector2 end;
    }
}
