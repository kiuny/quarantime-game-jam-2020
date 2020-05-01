﻿using System.Collections.Generic;
using System.Linq;
using Runtime.Terrain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Runtime.Map
{
    [ExecuteInEditMode]
    public class TerrainGenerator : MonoBehaviour
    {
        private const string meshHolderName = "Terrain Mesh";

        public bool autoUpdate = true;

        public bool centralize = true;
        public int worldSize = 20;

        public NoiseSettings terrainNoise;

        public Biome[] biomes;

        private MeshFilter[] _meshFilters;
        private MeshRenderer[] _meshRenderers;
        private Mesh[] _meshes;

        private bool _needsUpdate;

        private void Start()
        {
            Generate();
        }

        private void Update()
        {
            if (!_needsUpdate || !autoUpdate) return;
            _needsUpdate = false;
            Generate();
        }

        public TerrainData Generate()
        {
            CreateMeshComponents();

            var numTilesPerLine = Mathf.CeilToInt(worldSize);
            var min = centralize ? -numTilesPerLine / 2f : 0;
            var map = HeightmapGenerator.GenerateHeightmap(terrainNoise, numTilesPerLine);

            var vertices = biomes.Select(_ => new List<Vector3>()).ToArray();
            var triangles = biomes.Select(_ => new List<int>()).ToArray();
            var normals = biomes.Select(_ => new List<Vector3>()).ToArray();

            // Some convenience stuff:
            var upVectors = new[] {Vector3.up, Vector3.up, Vector3.up, Vector3.up};
            var cardinalsDx = new[] {0, 0, -1, 1};
            var cardinalsDy = new[] {1, -1, 0, 0};
            var sideVertexIndexByDir = new[] {new[] {0, 1}, new[] {3, 2}, new[] {2, 0}, new[] {1, 3}};
            var sideNormalsByDir = new[]
            {
                new[] {Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward},
                new[] {Vector3.back, Vector3.back, Vector3.back, Vector3.back},
                new[] {Vector3.left, Vector3.left, Vector3.left, Vector3.left},
                new[] {Vector3.right, Vector3.right, Vector3.right, Vector3.right}
            };

            // Terrain data:
            var terrainData = new TerrainData(numTilesPerLine);
            var colors = biomes.Select(_ => new List<Color>()).ToArray();

            for (var y = 0; y < numTilesPerLine; y++)
            {
                for (var x = 0; x < numTilesPerLine; x++)
                {
                    var biomeAndStep = GetBiomeInfo(map[x, y]);
                    var biomeIndex = (int) biomeAndStep.x;
                    var biome = biomes[biomeIndex];
                    terrainData.BiomeIndices[x, y] = biomeIndex;
                    terrainData.BiomesStep[x, y] = biomeAndStep.y;
                    terrainData.Depths[x, y] = Mathf.Lerp(biome.startDepth, biome.endDepth, biomeAndStep.y);
                }
            }

            // Bridge gaps between water and land tiles, and also fill in sides of map
            for (var y = 0; y < numTilesPerLine; y++)
            {
                for (var x = 0; x < numTilesPerLine; x++)
                {
                    var biomeIndex = terrainData.BiomeIndices[x, y];
                    var biome = biomes[biomeIndex];
                    var step = terrainData.BiomesStep[x, y];
                    var color = Color.Lerp(biome.startColor, biome.endColor, step);
                    var height = terrainData.Depths[x, y];

                    var verticesColor = new[] {color, color, color, color};
                    colors[biomeIndex].AddRange(verticesColor);

                    // Vertices
                    var vertexIndex = vertices[biomeIndex].Count;
                    var northWest = new Vector3(min + x, height, min + y + 1);
                    var northEast = northWest + Vector3.right;
                    var southWest = northWest - Vector3.forward;
                    var southEast = southWest + Vector3.right;
                    var cornerVertices = new[] {northWest, northEast, southWest, southEast};
                    vertices[biomeIndex].AddRange(cornerVertices);
                    normals[biomeIndex].AddRange(upVectors);
                    triangles[biomeIndex].AddRange(new[] {vertexIndex, vertexIndex + 1, vertexIndex + 2});
                    triangles[biomeIndex].AddRange(new[] {vertexIndex + 1, vertexIndex + 3, vertexIndex + 2});

                    if (biomeIndex == 0) continue;

                    for (var i = 0; i < 4; i++)
                    {
                        var neighbourX = x + cardinalsDx[i];
                        var neighbourY = y + cardinalsDy[i];

                        var neighbourIsOutOfBounds = neighbourX < 0 || neighbourX >= numTilesPerLine || neighbourY < 0 || neighbourY >= numTilesPerLine;

                        if (neighbourIsOutOfBounds) continue;

                        var depthOfNeighbour = terrainData.Depths[neighbourX, neighbourY] - height;

                        if (depthOfNeighbour >= 0f) continue;

                        vertexIndex = vertices[biomeIndex].Count;
                        var edgeVertexIndexA = sideVertexIndexByDir[i][0];
                        var edgeVertexIndexB = sideVertexIndexByDir[i][1];
                        vertices[biomeIndex].Add(cornerVertices[edgeVertexIndexA]);
                        vertices[biomeIndex].Add(cornerVertices[edgeVertexIndexA] + Vector3.up * depthOfNeighbour);
                        vertices[biomeIndex].Add(cornerVertices[edgeVertexIndexB]);
                        vertices[biomeIndex].Add(cornerVertices[edgeVertexIndexB] + Vector3.up * depthOfNeighbour);

                        colors[biomeIndex].AddRange(verticesColor);
                        triangles[biomeIndex].AddRange(new[] {vertexIndex, vertexIndex + 1, vertexIndex + 2, vertexIndex + 1, vertexIndex + 3, vertexIndex + 2});
                        normals[biomeIndex].AddRange(sideNormalsByDir[i]);
                    }

                    // Terrain data:
                }
            }

            // Update mesh:
            for (int biomeIndex = 0; biomeIndex < biomes.Length; biomeIndex++)
            {
                _meshes[biomeIndex].SetVertices(vertices[biomeIndex]);
                _meshes[biomeIndex].SetTriangles(triangles[biomeIndex], 0, true);
                _meshes[biomeIndex].SetColors(colors[biomeIndex]);
                _meshes[biomeIndex].SetNormals(normals[biomeIndex]);

                _meshRenderers[biomeIndex].sharedMaterial = biomes[biomeIndex].material;
            }

            return terrainData;
        }

        private Vector2 GetBiomeInfo(float height)
        {
            // Find current biome
            var biomeIndex = 0;
            float biomeStartHeight = 0;
            for (var i = 0; i < biomes.Length; i++)
            {
                if (height <= biomes[i].height)
                {
                    biomeIndex = i;
                    break;
                }

                biomeStartHeight = biomes[i].height;
            }

            var biome = biomes[biomeIndex];
            var sampleT = Mathf.InverseLerp(biomeStartHeight, biome.height, height);
            sampleT = (int) (sampleT * biome.numSteps) / (float) Mathf.Max(biome.numSteps, 1);

            // UV stores x: biomeIndex and y: val between 0 and 1 for how close to prev/next biome
            return new Vector2(biomeIndex, sampleT);
        }

        private void CreateMeshComponents()
        {
            _meshFilters = new MeshFilter[biomes.Length];
            _meshRenderers = new MeshRenderer[biomes.Length];
            _meshes = new Mesh[biomes.Length];

            for (var biomeIndex = 0; biomeIndex < biomes.Length; biomeIndex++)
            {
                if (_meshFilters[biomeIndex] == null)
                {
                    var terrainMeshName = meshHolderName + "_" + biomeIndex;
                    var holder = GameObject.Find(terrainMeshName);
                    if (holder)
                    {
                        _meshFilters[biomeIndex] = holder.GetComponent<MeshFilter>();
                        _meshRenderers[biomeIndex] = holder.GetComponent<MeshRenderer>();
                    }
                    else
                    {
                        holder = new GameObject(terrainMeshName);
                        _meshRenderers[biomeIndex] = holder.AddComponent<MeshRenderer>();
                        _meshFilters[biomeIndex] = holder.AddComponent<MeshFilter>();
                        holder.AddComponent<MeshCollider>();
                    }
                }

                if (_meshFilters[biomeIndex].sharedMesh == null)
                {
                    _meshes[biomeIndex] = new Mesh();
                    _meshes[biomeIndex].indexFormat = IndexFormat.UInt32;
                    _meshFilters[biomeIndex].sharedMesh = _meshes[biomeIndex];
                }
                else
                {
                    _meshes[biomeIndex] = _meshFilters[biomeIndex].sharedMesh;
                    _meshes[biomeIndex].Clear();
                }

                _meshRenderers[biomeIndex].shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private void OnValidate()
        {
            _needsUpdate = true;
        }


        public class TerrainData
        {
            public int Size;
            public Vector3[,] TileCentres;
            public int[,] BiomeIndices;
            public float[,] BiomesStep;
            public float[,] Depths;

            public TerrainData(int size)
            {
                this.Size = size;
                TileCentres = new Vector3[size, size];
                BiomeIndices = new int[size, size];
                BiomesStep = new float[size, size];
                Depths = new float[size, size];
            }
        }
    }
}