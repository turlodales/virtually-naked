﻿using System;
using System.Collections.Generic;

public class OcclusionBinding {
	public static OcclusionBinding MakeForFigure(string figureName, Geometry geometry, BoneSystem boneSystem, SkinBinding skinBinding) {
		var surrogates = HemisphereOcclusionSurrogate.MakeForFigure(figureName, geometry, boneSystem, skinBinding);
		return new OcclusionBinding(geometry.VertexCount, surrogates);
	}
	
	private int vertexCount;
	private List<HemisphereOcclusionSurrogate> surrogates;
	
	public OcclusionBinding(int vertexCount, List<HemisphereOcclusionSurrogate> surrogates) {
		this.vertexCount = vertexCount;
		this.surrogates = surrogates;
	}
	
	public List<HemisphereOcclusionSurrogate> Surrogates => surrogates;
	
	public int[] MakeSurrogateMap() {
		int[] map = new int[vertexCount];
		for (int surrogateIdx = 0; surrogateIdx < surrogates.Count; ++surrogateIdx) {
			HemisphereOcclusionSurrogate surrogate = surrogates[surrogateIdx];
			foreach (var vertexIdx in surrogate.AttachedVertices) {
				if (map[vertexIdx] != 0) {
					throw new Exception("surrogate map conflict");
				}

				map[vertexIdx] = surrogateIdx + 1;
			}
		}

		return map;
	}

	public OcclusionSurrogateParameters[] MakeSurrogateParameters() {
		OcclusionSurrogateParameters[] parameters = new OcclusionSurrogateParameters[surrogates.Count];

		int offset = vertexCount;
		for (int surrogateIdx = 0; surrogateIdx < surrogates.Count; ++surrogateIdx) {
			HemisphereOcclusionSurrogate surrogate = surrogates[surrogateIdx];
			parameters[surrogateIdx] = new OcclusionSurrogateParameters(
				surrogate.AttachedBone.Index,
				offset);

			offset += surrogate.SampleCount;
		}

		return parameters;
	}
}