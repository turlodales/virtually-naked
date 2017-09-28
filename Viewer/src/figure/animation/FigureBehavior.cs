﻿using Newtonsoft.Json;
using System.Collections.Generic;
using SharpDX;

public class FigureBehavior {
	public static FigureBehavior Load(ControllerManager controllerManager, IArchiveDirectory figureDir, FigureModel model) {
		InverterParameters inverterParameters = Persistance.Load<InverterParameters>(figureDir.File("inverter-parameters.dat"));
		return new FigureBehavior(controllerManager, model, inverterParameters);
	}

	private const float FramesPerSecond = 30 * FigureActiveSettings.AnimationSpeed;
	
	private readonly FigureModel model;
	private readonly Poser poser;
	private readonly InverseKinematicsAnimator ikAnimator;
	private readonly IProceduralAnimator proceduralAnimator;
	private readonly DragHandle dragHandle;

	public FigureBehavior(ControllerManager controllerManager, FigureModel model, InverterParameters inverterParameters) {
		this.model = model;
		poser = new Poser(model.Definition);
		ikAnimator = new InverseKinematicsAnimator(controllerManager, model.Definition, inverterParameters);
		proceduralAnimator = new StandardProceduralAnimator(model.Definition, model.Behavior);
		dragHandle = new DragHandle(controllerManager, FigureActiveSettings.InitialTransform);
	}
	
	private Pose GetBlendedPose(float time) {
		var posesByFrame = model.Animation.ActiveAnimation.PosesByFrame;

		float unloopedFrameIdx = time * FramesPerSecond;
 		float currentFrameIdx = unloopedFrameIdx % posesByFrame.Count;

		int baseFrameIdx = (int) currentFrameIdx;
		Pose prevFramePose = posesByFrame[IntegerUtils.Mod(baseFrameIdx + 0, posesByFrame.Count)];
		Pose nextFramePose = posesByFrame[IntegerUtils.Mod(baseFrameIdx + 1, posesByFrame.Count)];
		
		var poseBlender = new PoseBlender(model.Definition.BoneSystem.Bones.Count);
		float alpha = currentFrameIdx - baseFrameIdx;
		poseBlender.Add(1 - alpha, prevFramePose);
		poseBlender.Add(alpha, nextFramePose);
		var blendedPose = poseBlender.GetResult();
		return blendedPose;
	}

	public ChannelInputs Update(FrameUpdateParameters updateParameters, ControlVertexInfo[] previousFrameControlVertexInfos) {
		ChannelInputs inputs = new ChannelInputs(model.Inputs);

		dragHandle.Update();
		DualQuaternion rootTransform = DualQuaternion.FromMatrix(dragHandle.Transform);
		
		var blendedPose = GetBlendedPose(updateParameters.Time);
		poser.Apply(inputs, blendedPose, rootTransform);

		ikAnimator.Update(inputs, previousFrameControlVertexInfos);

		proceduralAnimator.Update(updateParameters, inputs);

		return inputs;
	}

	public class PoseRecipe {
		[JsonProperty("rotation")]
		public float[] rotation;

		[JsonProperty("translation")]
		public float[] translation;

		[JsonProperty("bone-rotations")]
		public Dictionary<string, float[]> boneRotations;

		public void Merge(FigureBehavior behaviour) {
			Vector3 rootRotation = new Vector3(rotation);
			Vector3 rootTranslation = new Vector3(translation);
			DualQuaternion rootTransform = DualQuaternion.FromRotationTranslation(
				behaviour.model.Definition.BoneSystem.RootBone.RotationOrder.FromAngles(MathExtensions.DegreesToRadians(rootRotation)),
				rootTranslation);
			behaviour.dragHandle.Transform = rootTransform.ToMatrix();

			var inputs = behaviour.ikAnimator.InputDeltas;
			inputs.ClearToZero();
			foreach (var bone in behaviour.model.Definition.BoneSystem.Bones) {
				Vector3 angles;
				if (boneRotations.TryGetValue(bone.Name, out var values)) {
					angles = new Vector3(values);
				} else {
					angles = Vector3.Zero;
				}
				bone.Rotation.SetValue(inputs, angles);
			}
		}
	}

	public PoseRecipe RecipizePose() {
		var rootTransform = DualQuaternion.FromMatrix(dragHandle.Transform);
		Vector3 rootRotation = MathExtensions.RadiansToDegrees(model.Definition.BoneSystem.RootBone.RotationOrder.ToAngles(rootTransform.Rotation));
		Vector3 rootTranslation = rootTransform.Translation;
		
		Dictionary<string, float[]> boneRotations = new Dictionary<string, float[]>();
		var inputs = ikAnimator.InputDeltas;
		foreach (var bone in model.Definition.BoneSystem.Bones) {
			var angles = bone.Rotation.GetInputValue(inputs);
			if (!angles.IsZero) {
				boneRotations.Add(bone.Name, angles.ToArray());
			}
		}

		return new PoseRecipe {
			rotation = rootRotation.ToArray(),
			translation = rootTranslation.ToArray(),
			boneRotations = boneRotations
		};
	}
}