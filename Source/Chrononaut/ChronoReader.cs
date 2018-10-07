using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using PartToolsLib;

namespace Chrononaut
{
	public static class ChronoReader
	{
		private static int fileVersion;
		private static UrlDir.UrlFile file;
		private static List<ChronoReader.MaterialDummy> matDummies;
		private static List<ChronoReader.BonesDummy> boneDummies;
		private static ChronoReader.TextureDummyList textureDummies;
		public static bool shaderFallback;

		public static GameObject Read(UrlDir.UrlFile file)
		{
			ChronoReader.file = file;
			BinaryReader reader = new BinaryReader(File.Open(file.fullPath, FileMode.Open));
			if (null == reader)
			{
				Debug.Log("File error");
				return null;
			}
			ChronoReader.matDummies = new List<ChronoReader.MaterialDummy>();
			ChronoReader.boneDummies = new List<ChronoReader.BonesDummy>();
			ChronoReader.textureDummies = new ChronoReader.TextureDummyList();
			FileType type = (FileType)reader.ReadInt32();
			ChronoReader.fileVersion = reader.ReadInt32();
			string object_name = reader.ReadString() + string.Empty;
			if (FileType.ModelBinary != type)
			{
				Debug.LogWarning("File '" + file.fullPath + "' is an incorrect type.");
				reader.Close();
				return null;
			}
			GameObject o = null;
			try
			{
				o = ChronoReader.ReadChild(reader, null);
				if (null != ChronoReader.boneDummies && ChronoReader.boneDummies.Count > 0)
				{
					for (int i = 0, count = ChronoReader.boneDummies.Count; i < count; ++i)
					{
						Transform[] transformArray = new Transform[ChronoReader.boneDummies[i].bones.Count];
						for (int j = 0, count_j = ChronoReader.boneDummies[i].bones.Count; j < count_j; ++j)
							transformArray[j] = ChronoReader.FindChildByName(o.transform, ChronoReader.boneDummies[i].bones[j]);
						ChronoReader.boneDummies[i].smr.bones = transformArray;
					}
				}
				if (ChronoReader.shaderFallback)
				{
					Renderer[] componentsInChildren = (Renderer[])o.GetComponentsInChildren<Renderer>();
					for (int i = 0, len_i = componentsInChildren.Length; i < len_i; ++i)
					{
						Renderer renderer = componentsInChildren[i];
						for (int j = 0, len_j = renderer.sharedMaterials.Length; j < len_j; ++j)
							renderer.sharedMaterials[j].shader = Shader.Find("KSP/Diffuse");
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogError("File error:\n" + ex.Message + "\n" + ex.StackTrace);
			}
			ChronoReader.boneDummies = null;
			ChronoReader.matDummies = null;
			ChronoReader.textureDummies = null;
			reader.Close();
			return o;
		}

		private static GameObject ReadChild(BinaryReader br, Transform parent)
		{
			GameObject o = new GameObject(br.ReadString());
			o.transform.parent = parent;
			o.transform.localPosition = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
			o.transform.localRotation = new Quaternion(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
			o.transform.localScale = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
			while (-1 != br.PeekChar())
			{
				switch ((EntryType)br.ReadInt32())
				{
					case EntryType.ChildTransformStart:
						ChronoReader.ReadChild(br, o.transform);
						break;
					case EntryType.ChildTransformEnd:
						return o;
					case EntryType.Animation:
						if (ChronoReader.fileVersion >= 3)
						{
							ChronoReader.ReadAnimation(br, o);
							break;
						}
						ChronoReader.ReadAnimation(br, o);
						break;
					case EntryType.MeshCollider: {
						MeshCollider collider = (MeshCollider)o.AddComponent<MeshCollider>();
						collider.convex = br.ReadBoolean();
						if (!collider.convex)
							collider.convex = true;
						collider.sharedMesh = ChronoReader.ReadMesh(br);
						} break;
					case EntryType.SphereCollider: {
						SphereCollider collider = (SphereCollider)o.AddComponent<SphereCollider>();
						collider.radius = br.ReadSingle();
						collider.center = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
						} break;
					case EntryType.CapsuleCollider: {
						CapsuleCollider collider = (CapsuleCollider)o.AddComponent<CapsuleCollider>();
						collider.radius = br.ReadSingle();
						collider.direction = br.ReadInt32();
						collider.center = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
						} break;
					case EntryType.BoxCollider: {
						BoxCollider collider = (BoxCollider)o.AddComponent<BoxCollider>();
						collider.size = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
						collider.center = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
						} break;
					case EntryType.MeshFilter:
						((MeshFilter)o.AddComponent<MeshFilter>()).sharedMesh = ChronoReader.ReadMesh(br);
						break;
					case EntryType.MeshRenderer:
						ChronoReader.ReadMeshRenderer(br, o);
						break;
					case EntryType.SkinnedMeshRenderer:
						ChronoReader.ReadSkinnedMeshRenderer(br, o);
						break;
					case EntryType.Materials: {
						int num = br.ReadInt32();
						for (int i = 0; i < num; ++i)
						{
							ChronoReader.MaterialDummy mat = ChronoReader.matDummies[i];
							Material material = ChronoReader.fileVersion < 4 ? ChronoReader.ReadMaterial(br) : ChronoReader.ReadMaterial4(br);
							int count = mat.renderers.Count;
							while (count-- > 0)
								mat.renderers[count].sharedMaterial = material;
							for (int j = 0, count_j = mat.particleEmitters.Count; j < count_j; ++j)
								mat.particleEmitters[j].material = material;
						}
						} break;
					case EntryType.Textures:
						ChronoReader.ReadTextures(br, o);
						break;
					case EntryType.Light:
						ChronoReader.ReadLight(br, o);
						break;
					case EntryType.TagAndLayer:
						ChronoReader.ReadTagAndLayer(br, o);
						break;
					case EntryType.MeshCollider2: {
						MeshCollider collider = (MeshCollider)o.AddComponent<MeshCollider>();
						bool flag = br.ReadBoolean();
						collider.convex = br.ReadBoolean();
						((Collider)collider).isTrigger = flag;
						if (!collider.convex)
							collider.convex = true;
						collider.sharedMesh = ChronoReader.ReadMesh(br);
						} break;
					case EntryType.SphereCollider2:
						SphereCollider sphereCollider2 = (SphereCollider)o.AddComponent<SphereCollider>();
						((Collider)sphereCollider2).isTrigger = br.ReadBoolean();
						sphereCollider2.radius = br.ReadSingle();
						sphereCollider2.center = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
						break;
					case EntryType.CapsuleCollider2: {
						CapsuleCollider collider = (CapsuleCollider)o.AddComponent<CapsuleCollider>();
						((Collider)collider).isTrigger = br.ReadBoolean();
						collider.radius = br.ReadSingle();
						collider.height = br.ReadSingle();
						collider.direction = br.ReadInt32();
						collider.center = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
						} break;
					case EntryType.BoxCollider2: {
						BoxCollider collider = (BoxCollider)o.AddComponent<BoxCollider>();
						((Collider)collider).isTrigger = br.ReadBoolean();
						collider.size = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
						collider.center = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
						} break;
					case EntryType.WheelCollider: {
						WheelCollider wheelCollider1 = (WheelCollider)o.AddComponent<WheelCollider>();
						wheelCollider1.mass = br.ReadSingle();
						wheelCollider1.radius = br.ReadSingle();
						wheelCollider1.suspensionDistance = br.ReadSingle();
						wheelCollider1.center = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
						WheelCollider wheelCollider2 = wheelCollider1;
						JointSpring jointSpring1 = new JointSpring
						{
							spring = br.ReadSingle(),
							damper = br.ReadSingle(),
							targetPosition = br.ReadSingle()
						};
						JointSpring jointSpring2 = jointSpring1;
						wheelCollider2.suspensionSpring = jointSpring2;
						WheelCollider wheelCollider3 = wheelCollider1;
						WheelFrictionCurve wheelFrictionCurve1 = new WheelFrictionCurve
						{
							extremumSlip = br.ReadSingle(),
							extremumValue = br.ReadSingle(),
							asymptoteSlip = br.ReadSingle(),
							asymptoteValue = br.ReadSingle(),
							stiffness = br.ReadSingle()
						};
						WheelFrictionCurve wheelFrictionCurve2 = wheelFrictionCurve1;
						wheelCollider3.forwardFriction = wheelFrictionCurve2;
						WheelCollider wheelCollider4 = wheelCollider1;
						wheelFrictionCurve1 = new WheelFrictionCurve
						{
							extremumSlip = br.ReadSingle(),
							extremumValue = br.ReadSingle(),
							asymptoteSlip = br.ReadSingle(),
							asymptoteValue = br.ReadSingle(),
							stiffness = br.ReadSingle()
						};
						WheelFrictionCurve wheelFrictionCurve3 = wheelFrictionCurve1;
						wheelCollider4.sidewaysFriction = wheelFrictionCurve3;
						((Collider)wheelCollider1).enabled = false;
						} break;
					case EntryType.Camera:
						ChronoReader.ReadCamera(br, o);
						break;
					case EntryType.ParticleEmitter:
						ChronoReader.ReadParticles(br, o);
						break;
				}
			}
			return o;
		}

		private static void ReadTextures(BinaryReader br, GameObject o)
		{
			int texture_count = br.ReadInt32();
			if (texture_count != ChronoReader.textureDummies.Count)
			{
				Debug.LogError("TextureError: " + texture_count + " " + textureDummies.Count);
			}
			else
			{
				for (int i = 0; i < texture_count; ++i)
				{
					string name = Path.GetFileNameWithoutExtension(br.ReadString());
					TextureType textureType = (TextureType)br.ReadInt32();
					Texture2D texture = GameDatabase.Instance.GetTexture(ChronoReader.file.parent.url + "/" + name, textureType == TextureType.NormalMap);
					if (null == texture)
					{
						Debug.LogError("Texture '" + ChronoReader.file.parent.url + "/" + name + "' not found!");
					}
					else
					{
						for (int j = 0, count_j = ChronoReader.textureDummies[i].Count; j < count_j; ++j)
						{
							ChronoReader.TextureMaterialDummy tex = ChronoReader.textureDummies[i][j];
							for (int k = 0, count_k = tex.shaderName.Count; k < count_k; ++k)
							{
								string str = tex.shaderName[k];
								tex.material.SetTexture(str, (Texture)texture);
							}
						}
					}
				}
			}
		}

		public static Texture2D NormalMapToUnityNormalMap(Texture2D tex)
		{
			int width = ((Texture)tex).width;
			int height = ((Texture)tex).height;
			Texture2D texture2D = new Texture2D(width, height, (TextureFormat)4, true)
			{
				wrapMode = 0
			};
			Color color = new Color(1f, 1f, 1f, 1f);
			for (int i = 0; i < width; ++i)
			{
				for (int j = 0; j < height; ++j)
				{
					Color pixel = tex.GetPixel(i, j);
					color.r = pixel.g;
					color.g = pixel.g;
					color.b = pixel.g;
					color.a = pixel.r;
					texture2D.SetPixel(i, j, color);
				}
			}
			texture2D.Apply(true, true);
			return texture2D;
		}

		private static void ReadMeshRenderer(BinaryReader br, GameObject o)
		{
			MeshRenderer meshRenderer = (MeshRenderer)o.AddComponent<MeshRenderer>();
			if (ChronoReader.fileVersion >= 1)
			{
				meshRenderer.shadowCastingMode = br.ReadBoolean() ? ShadowCastingMode.On : ShadowCastingMode.Off;
				meshRenderer.receiveShadows = br.ReadBoolean();
			}
			int renderer_count = br.ReadInt32();
			for (int i = 0; i < renderer_count; ++i)
			{
				int mat_count = br.ReadInt32();
				while (mat_count >= ChronoReader.matDummies.Count)
					ChronoReader.matDummies.Add(new ChronoReader.MaterialDummy());
				ChronoReader.matDummies[mat_count].renderers.Add(meshRenderer);
			}
		}

		private static void ReadSkinnedMeshRenderer(BinaryReader br, GameObject o)
		{
			SkinnedMeshRenderer skinnedMeshRenderer = (SkinnedMeshRenderer)o.AddComponent<SkinnedMeshRenderer>();
			int renderer_count = br.ReadInt32();
			for (int index1 = 0; index1 < renderer_count; ++index1)
			{
				int i = br.ReadInt32();
				while (i >= ChronoReader.matDummies.Count)
					ChronoReader.matDummies.Add(new ChronoReader.MaterialDummy());
				ChronoReader.matDummies[i].renderers.Add(skinnedMeshRenderer);
			}
			skinnedMeshRenderer.localBounds = new Bounds(new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()), new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
			skinnedMeshRenderer.quality = (SkinQuality)br.ReadInt32();
			skinnedMeshRenderer.updateWhenOffscreen = br.ReadBoolean();
			int num_bones = br.ReadInt32();
			ChronoReader.BonesDummy bones = new ChronoReader.BonesDummy
			{
				smr = skinnedMeshRenderer
			};
			for (int i = 0; i < num_bones; ++i)
				bones.bones.Add(br.ReadString());
			ChronoReader.boneDummies.Add(bones);
			skinnedMeshRenderer.sharedMesh = ChronoReader.ReadMesh(br);
		}

		private static Mesh ReadMesh(BinaryReader br)
		{
			Mesh mesh = new Mesh();
			if ((EntryType)br.ReadInt32() != EntryType.MeshStart)
			{
				Debug.LogError("Mesh Error");
				return null;
			}
			int mesh_count = br.ReadInt32();
			br.ReadInt32();
			int i = 0;
			EntryType entryType;
			while (EntryType.MeshEnd != (entryType = (EntryType)br.ReadInt32()) )
			{
				switch (entryType)
				{
					case EntryType.MeshVerts: {
						Vector3[] v = new Vector3[mesh_count];
						for (int j = 0; j < mesh_count; ++j)
							v[j] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
						mesh.vertices = v ;
						} break;
					case EntryType.MeshUV: {
						Vector2[] v = new Vector2[mesh_count];
						for (int j = 0; j < mesh_count; ++j)
							v[j] = new Vector2(br.ReadSingle(), br.ReadSingle());
						mesh.uv = v;
						} break;
					case EntryType.MeshUV2: {
						Vector2[] v = new Vector2[mesh_count];
						for (int j = 0; j < mesh_count; ++j)
							v[j] = new Vector2(br.ReadSingle(), br.ReadSingle());
						mesh.uv2 = v;
						}break;
					case EntryType.MeshNormals: {
						Vector3[] v = new Vector3[mesh_count];
						for (int j = 0; j < mesh_count; ++j)
							v[j] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
						mesh.normals = v;
						} break;
					case EntryType.MeshTangents: {
						Vector4[] v = new Vector4[mesh_count];
						for (int j = 0; j < mesh_count; ++j)
							v[j] = new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
						mesh.tangents = v;
						} break;
					case EntryType.MeshTriangles: {
						int triangle_count = br.ReadInt32();
						int[] triangles = new int[triangle_count];
						for (int j = 0; j < triangle_count; ++j)
							triangles[j] = br.ReadInt32();
						if (mesh.subMeshCount == i)
						{
							Mesh mesh2 = mesh;
							mesh2.subMeshCount = mesh2.subMeshCount + 1;
						}
						mesh.SetTriangles(triangles, i);
						++i;
						} break;
					case EntryType.MeshBoneWeights: {
						BoneWeight[] a = new BoneWeight[mesh_count];
						for (int j = 0; j < mesh_count; ++j)
						{
							a[j] = new BoneWeight
							{
								boneIndex0 = br.ReadInt32(),
								weight0 = br.ReadSingle(),
								boneIndex1 = br.ReadInt32(),
								weight1 = br.ReadSingle(),
								boneIndex2 = br.ReadInt32(),
								weight2 = br.ReadSingle(),
								boneIndex3 = br.ReadInt32(),
								weight3 = br.ReadSingle()
							};
						}
						mesh.boneWeights = a;
						} break;
					case EntryType.MeshBindPoses: {
						int count = br.ReadInt32();
						Matrix4x4[] m = new Matrix4x4[count];
						for (int j = 0; j < count; ++j)
						{
							m[j] = new Matrix4x4
							{
								m00 = br.ReadSingle(),
								m01 = br.ReadSingle(),
								m02 = br.ReadSingle(),
								m03 = br.ReadSingle(),
								m10 = br.ReadSingle(),
								m11 = br.ReadSingle(),
								m12 = br.ReadSingle(),
								m13 = br.ReadSingle(),
								m20 = br.ReadSingle(),
								m21 = br.ReadSingle(),
								m22 = br.ReadSingle(),
								m23 = br.ReadSingle(),
								m30 = br.ReadSingle(),
								m31 = br.ReadSingle(),
								m32 = br.ReadSingle(),
								m33 = br.ReadSingle()
							};
						}
						mesh.bindposes = m;
						} break;
					case EntryType.MeshVertexColors: 
						{
							Color32[] a = new Color32[mesh_count];
							for (int j = 0; j < mesh_count; ++j)
								a[j] = new Color32(br.ReadByte(), br.ReadByte(), br.ReadByte(), br.ReadByte());
							mesh.colors32 = a;
							break;
						}
					default:
						break;
				}
			}
			mesh.RecalculateBounds();
			return mesh;
		}

		private static Material ReadMaterial(BinaryReader br)
		{
			string name = br.ReadString();
			Material mat;
			switch ((ShaderType)br.ReadInt32())
			{
				case ShaderType.Specular:
					mat = new Material(Shader.Find("KSP/Specular"));
					ChronoReader.ReadMaterialTexture(br, mat, "_MainTex");
					mat.SetColor("_SpecColor", ChronoReader.ReadColor(br));
					mat.SetFloat("_Shininess", br.ReadSingle());
					break;
				case ShaderType.Bumped:
					mat = new Material(Shader.Find("KSP/Bumped"));
					ChronoReader.ReadMaterialTexture(br, mat, "_MainTex");
					ChronoReader.ReadMaterialTexture(br, mat, "_BumpMap");
					break;
				case ShaderType.BumpedSpecular:
					mat = new Material(Shader.Find("KSP/Bumped Specular"));
					ChronoReader.ReadMaterialTexture(br, mat, "_MainTex");
					ChronoReader.ReadMaterialTexture(br, mat, "_BumpMap");
					mat.SetColor("_SpecColor", ChronoReader.ReadColor(br));
					mat.SetFloat("_Shininess", br.ReadSingle());
					break;
				case ShaderType.Emissive:
					mat = new Material(Shader.Find("KSP/Emissive/Diffuse"));
					ChronoReader.ReadMaterialTexture(br, mat, "_MainTex");
					ChronoReader.ReadMaterialTexture(br, mat, "_Emissive");
					mat.SetColor((int)PropertyIDs._EmissiveColor, ChronoReader.ReadColor(br));
					break;
				case ShaderType.EmissiveSpecular:
					mat = new Material(Shader.Find("KSP/Emissive/Specular"));
					ChronoReader.ReadMaterialTexture(br, mat, "_MainTex");
					mat.SetColor("_SpecColor", ChronoReader.ReadColor(br));
					mat.SetFloat("_Shininess", br.ReadSingle());
					ChronoReader.ReadMaterialTexture(br, mat, "_Emissive");
					mat.SetColor((int)PropertyIDs._EmissiveColor, ChronoReader.ReadColor(br));
					break;
				case ShaderType.EmissiveBumpedSpecular:
					mat = new Material(Shader.Find("KSP/Emissive/Bumped Specular"));
					ChronoReader.ReadMaterialTexture(br, mat, "_MainTex");
					ChronoReader.ReadMaterialTexture(br, mat, "_BumpMap");
					mat.SetColor("_SpecColor", ChronoReader.ReadColor(br));
					mat.SetFloat("_Shininess", br.ReadSingle());
					ChronoReader.ReadMaterialTexture(br, mat, "_Emissive");
					mat.SetColor((int)PropertyIDs._EmissiveColor, ChronoReader.ReadColor(br));
					break;
				case ShaderType.AlphaCutout:
					mat = new Material(Shader.Find("KSP/Alpha/Cutoff"));
					ChronoReader.ReadMaterialTexture(br, mat, "_MainTex");
					mat.SetFloat("_Cutoff", br.ReadSingle());
					break;
				case ShaderType.AlphaCutoutBumped:
					mat = new Material(Shader.Find("KSP/Alpha/Cutoff Bumped"));
					ChronoReader.ReadMaterialTexture(br, mat, "_MainTex");
					ChronoReader.ReadMaterialTexture(br, mat, "_BumpMap");
					mat.SetFloat("_Cutoff", br.ReadSingle());
					break;
				case ShaderType.Alpha:
					mat = new Material(Shader.Find("KSP/Alpha/Translucent"));
					ChronoReader.ReadMaterialTexture(br, mat, "_MainTex");
					break;
				case ShaderType.AlphaSpecular:
					mat = new Material(Shader.Find("KSP/Alpha/Translucent Specular"));
					ChronoReader.ReadMaterialTexture(br, mat, "_MainTex");
					mat.SetFloat("_Gloss", br.ReadSingle());
					mat.SetColor("_SpecColor", ChronoReader.ReadColor(br));
					mat.SetFloat("_Shininess", br.ReadSingle());
					break;
				case ShaderType.AlphaUnlit:
					mat = new Material(Shader.Find("KSP/Alpha/Unlit Transparent"));
					ChronoReader.ReadMaterialTexture(br, mat, "_MainTex");
					mat.SetColor("_Color", ChronoReader.ReadColor(br));
					break;
				case ShaderType.Unlit:
					mat = new Material(Shader.Find("KSP/Unlit"));
					ChronoReader.ReadMaterialTexture(br, mat, "_MainTex");
					mat.SetColor("_Color", ChronoReader.ReadColor(br));
					break;
				case ShaderType.ParticleAlpha:
					mat = new Material(Shader.Find("KSP/Particles/Alpha Blended"));
					ChronoReader.ReadMaterialTexture(br, mat, "_MainTex");
					mat.SetColor("_Color", ChronoReader.ReadColor(br));
					mat.SetFloat("_InvFade", br.ReadSingle());
					break;
				case ShaderType.ParticleAdditive:
					mat = new Material(Shader.Find("KSP/Particles/Additive"));
					ChronoReader.ReadMaterialTexture(br, mat, "_MainTex");
					mat.SetColor("_Color", ChronoReader.ReadColor(br));
					mat.SetFloat("_InvFade", br.ReadSingle());
					break;
				default:
					mat = new Material(Shader.Find("KSP/Diffuse"));
					ChronoReader.ReadMaterialTexture(br, mat, "_MainTex");
					break;
			}
			if (null != mat)
				mat.name = name;
			return mat;
		}

		private static Material ReadMaterial4(BinaryReader br)
		{
			string material_name = br.ReadString();
			string shader_name = br.ReadString();
			int count = br.ReadInt32();
			Material mat = new Material(Shader.Find(shader_name))
			{
				name = material_name
			};
			for (int i = 0; i < count; ++i)
			{
				string textureName = br.ReadString();
				switch ((ShaderPropertyType)br.ReadInt32())
				{
					case ShaderPropertyType.Color:
						mat.SetColor(textureName, ChronoReader.ReadColor(br));
						break;
					case ShaderPropertyType.Vector:
						mat.SetVector(textureName, ChronoReader.ReadVector4(br));
						break;
					case ShaderPropertyType.Float:
						mat.SetFloat(textureName, br.ReadSingle());
						break;
					case ShaderPropertyType.Range:
						mat.SetFloat(textureName, br.ReadSingle());
						break;
					case ShaderPropertyType.TexEnv:
						ChronoReader.ReadMaterialTexture(br, mat, textureName);
						break;
				}
			}
			return mat;
		}

		private static void ReadMaterialTexture(BinaryReader br, Material mat, string textureName)
		{
			ChronoReader.textureDummies.AddTextureDummy(br.ReadInt32(), mat, textureName);
			Vector2 v = (Vector2)Vector3.zero;
			v.x = br.ReadSingle();
			v.y = br.ReadSingle();
			mat.SetTextureScale(textureName, v);
			v.x = br.ReadSingle();
			v.y = br.ReadSingle();
			mat.SetTextureOffset(textureName, v);
		}

		private static void ReadAnimation(BinaryReader br, GameObject o)
		{
			Animation animation = (Animation)o.AddComponent<Animation>();
			int animation_count = br.ReadInt32();
			for (int i = 0; i < animation_count; ++i)
			{
				AnimationClip animationClip = new AnimationClip
				{
					legacy = true
				};
				string animation_name = br.ReadString();
				animationClip.localBounds = new Bounds(new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()), new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
				animationClip.wrapMode = (WrapMode)br.ReadInt32();
				int frame_count = br.ReadInt32();
				for (int frame_i = 0; frame_i < frame_count; ++frame_i)
				{
					string relativePath = br.ReadString();
					string propertyName = br.ReadString();
					Type type = null;
					switch ((AnimationType)br.ReadInt32())
					{
						case AnimationType.Transform:
							type = typeof(Transform);
							break;
						case AnimationType.Material:
							type = typeof(Material);
							break;
						case AnimationType.Light:
							type = typeof(Light);
							break;
						case AnimationType.AudioSource:
							type = typeof(AudioSource);
							break;
					}
					WrapMode preWrap = (WrapMode)br.ReadInt32();
					WrapMode postWrap = (WrapMode)br.ReadInt32();
					int length = br.ReadInt32();
					Keyframe[] keyframeArray = new Keyframe[length];
					for (int index3 = 0; index3 < length; ++index3)
					{
						keyframeArray[index3] = new Keyframe
						{
							time = br.ReadSingle(),
							value = br.ReadSingle(),
							inTangent = br.ReadSingle(),
							outTangent = br.ReadSingle(),
							tangentMode = br.ReadInt32()
						};
					}
					AnimationCurve animationCurve = new AnimationCurve(keyframeArray)
					{
						preWrapMode = preWrap,
						postWrapMode = postWrap
					};
					if (null == relativePath || null == type || null == propertyName || null == animationCurve)
						Debug.Log(relativePath + ", " + type + ", " + propertyName + ", " + animationCurve);
					animationClip.SetCurve(relativePath, type, propertyName, animationCurve);
				}
				animation.AddClip(animationClip, animation_name);
			}
			string clip_name = br.ReadString();
			if (clip_name != string.Empty)
				animation.clip = animation.GetClip(clip_name);
			animation.playAutomatically = br.ReadBoolean();
		}

		private static void ReadAnimationEvents(BinaryReader br, GameObject o)
		{
			Animation animation = (Animation)o.AddComponent<Animation>();
			int count = br.ReadInt32();
			for (int i = 0; i < count; ++i)
			{
				AnimationClip aclip = new AnimationClip();
				string name = br.ReadString();
				aclip.localBounds = new Bounds(new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()), new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
				aclip.wrapMode = (WrapMode)br.ReadInt32();
				int animation_count = br.ReadInt32();
				for (int j = 0; j < animation_count; ++j)
				{
					string relativePath = br.ReadString();
					string propertyName = br.ReadString();
					Type type1 = null;
					switch ((AnimationType)br.ReadInt32())
					{
						case AnimationType.Transform:
							type1 = typeof(Transform);
							break;
						case AnimationType.Material:
							type1 = typeof(Material);
							break;
						case AnimationType.Light:
							type1 = typeof(Light);
							break;
						case AnimationType.AudioSource:
							type1 = typeof(AudioSource);
							break;
					}
					WrapMode preWrap = (WrapMode)br.ReadInt32();
					WrapMode postWrap = (WrapMode)br.ReadInt32();
					int keyFrame_count = br.ReadInt32();
					Keyframe[] a = new Keyframe[keyFrame_count];
					for (int k = 0; k < keyFrame_count; ++k)
					{
						a[k] = new Keyframe
						{
							time = br.ReadSingle(),
							value = br.ReadSingle(),
							inTangent = br.ReadSingle(),
							outTangent = br.ReadSingle(),
							tangentMode = br.ReadInt32()
						};
					}
					AnimationClip animationClip2 = aclip;
					Type type2 = type1;
					AnimationCurve animationCurve = new AnimationCurve(a)
					{
						preWrapMode = (preWrap),
						postWrapMode = (postWrap)
					};
					animationClip2.SetCurve(relativePath, type2, propertyName, animationCurve);
					int event_count = br.ReadInt32();
					Debug.Log("EVTS: " + event_count);
					for (int k = 0; k < event_count; ++k)
					{
						AnimationEvent ev = new AnimationEvent
						{
							time = br.ReadSingle(),
							functionName = br.ReadString(),
							stringParameter = br.ReadString(),
							intParameter = br.ReadInt32(),
							floatParameter = br.ReadSingle(),
							messageOptions = (SendMessageOptions)br.ReadInt32()
						};
						Debug.Log(ev.time);
						Debug.Log(ev.functionName);
						Debug.Log(ev.stringParameter);
						Debug.Log(ev.intParameter);
						Debug.Log(ev.floatParameter);
						Debug.Log(ev.messageOptions);
						aclip.AddEvent(ev);
					}
				}
				animation.AddClip(aclip, name);
			}
			string clip_name = br.ReadString();
			if (clip_name != string.Empty)
				animation.clip = animation.GetClip(clip_name);
			animation.playAutomatically = br.ReadBoolean();
		}

		private static void ReadLight(BinaryReader br, GameObject o)
		{
			Light light = o.AddComponent<Light>();
			light.type = (LightType)br.ReadInt32();
			light.intensity = br.ReadSingle();
			light.range = br.ReadSingle();
			light.color = ChronoReader.ReadColor(br);
			light.cullingMask = (br.ReadInt32());
			if (ChronoReader.fileVersion <= 1)
				return;
			light.spotAngle = br.ReadSingle();
		}

		private static void ReadTagAndLayer(BinaryReader br, GameObject o)
		{
			o.tag = br.ReadString();
			o.layer = br.ReadInt32();
		}

		private static void ReadCamera(BinaryReader br, GameObject o)
		{
			Camera camera = o.AddComponent<Camera>();
			camera.clearFlags = (CameraClearFlags)br.ReadInt32();
			camera.backgroundColor = ChronoReader.ReadColor(br);
			camera.cullingMask = br.ReadInt32();
			camera.orthographic = br.ReadBoolean();
			camera.fieldOfView = br.ReadSingle();
			camera.nearClipPlane = br.ReadSingle();
			camera.farClipPlane = br.ReadSingle();
			camera.depth = br.ReadSingle();
			camera.enabled = false;
		}

		private static void ReadParticles(BinaryReader br, GameObject o)
		{
			KSPParticleEmitter e = o.AddComponent<KSPParticleEmitter>();
			e.emit = br.ReadBoolean();
			e.shape = (KSPParticleEmitter.EmissionShape)br.ReadInt32();
			e.shape3D.x = br.ReadSingle();
			e.shape3D.y = br.ReadSingle();
			e.shape3D.z = br.ReadSingle();
			e.shape2D.x = br.ReadSingle();
			e.shape2D.y = br.ReadSingle();
			e.shape1D = br.ReadSingle();
			e.color = ChronoReader.ReadColor(br);
			e.useWorldSpace = br.ReadBoolean();
			e.minSize = br.ReadSingle();
			e.maxSize = br.ReadSingle();
			e.minEnergy = br.ReadSingle();
			e.maxEnergy = br.ReadSingle();
			e.minEmission = br.ReadInt32();
			e.maxEmission = br.ReadInt32();
			e.worldVelocity.x = br.ReadSingle();
			e.worldVelocity.y = br.ReadSingle();
			e.worldVelocity.z = br.ReadSingle();
			e.localVelocity.x = br.ReadSingle();
			e.localVelocity.y = br.ReadSingle();
			e.localVelocity.z = br.ReadSingle();
			e.rndVelocity.x = br.ReadSingle();
			e.rndVelocity.y = br.ReadSingle();
			e.rndVelocity.z = br.ReadSingle();
			e.emitterVelocityScale = br.ReadSingle();
			e.angularVelocity = br.ReadSingle();
			e.rndAngularVelocity = br.ReadSingle();
			e.rndRotation = br.ReadBoolean();
			e.doesAnimateColor = br.ReadBoolean();
			e.colorAnimation = new Color[5];
			for (int j = 0; j < 5; ++j)
				e.colorAnimation[j] = ChronoReader.ReadColor(br);
			e.worldRotationAxis.x = br.ReadSingle();
			e.worldRotationAxis.y = br.ReadSingle();
			e.worldRotationAxis.z = br.ReadSingle();
			e.localRotationAxis.x = br.ReadSingle();
			e.localRotationAxis.y = br.ReadSingle();
			e.localRotationAxis.z = br.ReadSingle();
			e.sizeGrow = br.ReadSingle();
			e.rndForce.x = br.ReadSingle();
			e.rndForce.y = br.ReadSingle();
			e.rndForce.z = br.ReadSingle();
			e.force.x = br.ReadSingle();
			e.force.y = br.ReadSingle();
			e.force.z = br.ReadSingle();
			e.damping = br.ReadSingle();
			e.castShadows = br.ReadBoolean();
			e.recieveShadows = br.ReadBoolean();
			e.lengthScale = br.ReadSingle();
			e.velocityScale = br.ReadSingle();
			e.maxParticleSize = br.ReadSingle();
			switch (br.ReadInt32())
			{
				case 3:
					e.particleRenderMode = ParticleSystemRenderMode.Stretch;
					break;
				case 4:
					e.particleRenderMode = ParticleSystemRenderMode.HorizontalBillboard;
					break;
				case 5:
					e.particleRenderMode = ParticleSystemRenderMode.VerticalBillboard;
					break;
				default:
					e.particleRenderMode = ParticleSystemRenderMode.Billboard;
					break;
			}
			e.uvAnimationXTile = br.ReadInt32();
			e.uvAnimationYTile = br.ReadInt32();
			e.uvAnimationCycles = br.ReadInt32();
			int i = br.ReadInt32();
			while (i >= ChronoReader.matDummies.Count)
				ChronoReader.matDummies.Add(new ChronoReader.MaterialDummy());
			ChronoReader.matDummies[i].particleEmitters.Add(e);
		}

		public static Transform FindChildByName(Transform parent, string name)
		{
			if (parent.name == name)
				return parent;
			IEnumerator enumerator = parent.GetEnumerator();
			try
			{
				while (enumerator.MoveNext())
				{
					Transform childByName = ChronoReader.FindChildByName((Transform)enumerator.Current, name);
					if (null != childByName)
						return childByName;
				}
			}
			finally
			{
				if (enumerator is IDisposable disposable)
					disposable.Dispose();
			}
			return null;
		}

		private static Color ReadColor(BinaryReader br)
		{
			return new Color(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
		}

		private static Vector4 ReadVector2(BinaryReader br)
		{
			return new Vector2(br.ReadSingle(), br.ReadSingle());
		}

		private static Vector4 ReadVector3(BinaryReader br)
		{
			return new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
		}

		private static Vector4 ReadVector4(BinaryReader br)
		{
			return new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
		}

		public enum ShaderPropertyType
		{
			Color,
			Vector,
			Float,
			Range,
			TexEnv,
		}

		private class MaterialDummy
		{
			public List<Renderer> renderers;
			public List<KSPParticleEmitter> particleEmitters;

			public MaterialDummy()
			{
				this.renderers = new List<Renderer>();
				this.particleEmitters = new List<KSPParticleEmitter>();
			}
		}

		private class BonesDummy
		{
			public SkinnedMeshRenderer smr;
			public List<string> bones;

			public BonesDummy()
			{
				this.bones = new List<string>();
			}
		}

		private class TextureMaterialDummy
		{
			public Material material;
			public List<string> shaderName;

			public TextureMaterialDummy(Material material)
			{
				this.material = material;
				this.shaderName = new List<string>();
			}
		}

		private class TextureDummy : List<ChronoReader.TextureMaterialDummy>
		{
			public bool Contains(Material material)
			{
				int count = this.Count;
				while (count-- > 0)
				{
					if (this[count].material = material)
						return true;
				}
				return false;
			}

			public ChronoReader.TextureMaterialDummy Get(Material material)
			{
				int i = 0;
				for (int count = this.Count; i < count; ++i)
				{
					if (this[i].material = material)
						return this[i];
				}
				return null;
			}

			public void AddMaterialDummy(Material material, string shaderName)
			{
				ChronoReader.TextureMaterialDummy tex = this.Get(material);
				if (null == tex)
					this.Add(tex = new ChronoReader.TextureMaterialDummy(material));
				if (tex.shaderName.Contains(shaderName))
					return;
				tex.shaderName.Add(shaderName);
			}
		}

		private class TextureDummyList : List<ChronoReader.TextureDummy>
		{
			public void AddTextureDummy(int textureID, Material material, string shaderName)
			{
				if (-1 == textureID)
					return;
				while (textureID >= this.Count)
					this.Add(new ChronoReader.TextureDummy());
				this[textureID].AddMaterialDummy(material, shaderName);
			}
		}
	}
}
