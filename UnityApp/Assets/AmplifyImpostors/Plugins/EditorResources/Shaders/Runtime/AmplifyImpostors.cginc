// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

#ifndef AMPLIFYIMPOSTORS_INCLUDED
#define AMPLIFYIMPOSTORS_INCLUDED

#include "AmplifyImpostorsConfig.cginc"

#define AI_DEPTH_OPTIMIZATION // active by default; keep option only for debugging

#if defined( UNITY_REVERSED_Z ) && ( SHADER_TARGET >= 45 ) && defined( AI_DEPTH_OPTIMIZATION )
	#define AI_POSITION_QUALIFIERS linear noperspective centroid
	#define AI_DEPTH_SEMANTIC SV_DepthLessEqual
#else
	#define AI_POSITION_QUALIFIERS
	#define AI_DEPTH_SEMANTIC SV_Depth
#endif

#if ( defined( AI_HD_RENDERPIPELINE ) || defined( AI_LW_RENDERPIPELINE ) ) && !defined( AI_RENDERPIPELINE )
	#define AI_RENDERPIPELINE
#endif

float2 VectorToOctahedron( float3 N )
{
	N /= dot( 1.0, abs( N ) );
	if ( N.z <= 0 )
	{
		N.xy = ( 1 - abs( N.yx ) ) * ( N.xy >= 0 ? 1.0 : -1.0 );
	}
	return N.xy;
}

float2 VectorToHemiOctahedron( float3 N )
{
	N.xy /= dot( 1.0, abs( N ) );
	return float2( N.x + N.y, N.x - N.y );
}

float3 OctahedronToVector( float2 Oct )
{
	float3 N = float3( Oct, 1.0 - dot( 1.0, abs( Oct ) ) );
	if ( N.z < 0 )
	{
		N.xy = ( 1 - abs( N.yx ) ) * ( N.xy >= 0 ? 1.0 : -1.0 );
	}
	return normalize( N );
}

float3 HemiOctahedronToVector( float2 Oct )
{
	Oct = float2( Oct.x + Oct.y, Oct.x - Oct.y ) *0.5;
	float3 N = float3( Oct, 1 - dot( 1.0, abs( Oct ) ) );
	return normalize( N );
}

sampler2D _Albedo;
sampler2D _Normals;
#if defined( AI_RENDERPIPELINE )
	TEXTURE2D( _Specular );
	SAMPLER( sampler_Specular );
#else
	sampler2D _Specular;
#endif
sampler2D _Occlusion;
sampler2D _Emission;
sampler2D _Position;

CBUFFER_START( UnityPerMaterial )
	float _FramesX;
	float _FramesY;
	float _Frames;
	float _ImpostorSize;
	float _Parallax;
	float _TextureBias;
	float _ClipMask;
	float _DepthSize;
	float _AI_ShadowBias;
	float _AI_ShadowView;
	float _AI_ForwardBias;
	float4 _Offset;
	float4 _AI_SizeOffset;
	float4 _AI_BoundsMin;
	float4 _AI_BoundsSize;
#if defined( EFFECT_HUE_VARIATION )
	half4 _HueVariation;
#endif
CBUFFER_END

#if defined( AI_RENDERPIPELINE )
	#define AI_SAMPLEBIAS( textureName, samplerName, coord2, bias ) SAMPLE_TEXTURE2D_BIAS( textureName, samplerName, coord2, bias )
	#define AI_ObjectToWorld GetObjectToWorldMatrix()
	#define AI_WorldToObject GetWorldToObjectMatrix()

	#define AI_INV_TWO_PI  INV_TWO_PI
	#define AI_PI          PI
	#define AI_INV_PI      INV_PI
#else
	#define AI_SAMPLEBIAS( textureName, samplerName, coord2, bias ) tex2Dbias( textureName, float4( coord2, 0, bias ) )
	#define AI_ObjectToWorld unity_ObjectToWorld
	#define AI_WorldToObject unity_WorldToObject

	#define AI_INV_TWO_PI  UNITY_INV_TWO_PI
	#define AI_PI          UNITY_PI
	#define AI_INV_PI      UNITY_INV_PI
#endif

inline void RayPlaneIntersectionUV( float3 normal, float3 rayPosition, float3 rayDirection, out float2 uvs, out float3 localNormal )
{
	// n = normal
	// p0 = ( 0, 0, 0 ) assuming center as zero
	// l0 = ray position
	// l = ray direction
	// solving to:
	// t = distance along ray that intersects the plane = ( ( p0 - l0 ) . n ) / ( l . n )
	// p = intersection point

	float lDotN = dot( rayDirection, normal ); // l . n
	float p0l0DotN = dot( -rayPosition, normal ); // ( p0 - l0 ) . n

	float t = p0l0DotN / lDotN; // if > 0 then it's intersecting
	float3 p = rayDirection * t + rayPosition;

	// create frame UVs
	float3 upVector = float3( 0, 1, 0 );
	float3 tangent = normalize( cross( upVector, normal ) + float3( -0.001, 0, 0 ) );
	float3 bitangent = cross( tangent, normal );

	float frameX = dot( p, tangent );
	float frameZ = dot( p, bitangent );

	uvs = -float2( frameX, frameZ ); // why negative???

	if ( t <= 0.0 ) // not intersecting
	{
		uvs = 0;
	}

	float3x3 worldToLocal = float3x3( tangent, bitangent, normal ); // TBN (same as doing separate dots?, assembly looks the same)
	localNormal = normalize( mul( worldToLocal, rayDirection ) );
}

float4 ImpostorClipPosDepthBias( float4 clipPos, float4 viewPos )
{
#if defined( AI_DEPTH_OPTIMIZATION )
	const float depthOffset = _DepthSize * 0.5 * length( AI_ObjectToWorld[ 2 ].xyz );
	if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1.0 )
	{
		// Orthographic
		clipPos.z += UNITY_MATRIX_P[ 2 ][ 2 ] * depthOffset;
	}
	else
	{
		// Perspective
		float3 viewPosAdjusted = float3( viewPos.xy, min( viewPos.z + depthOffset, -_ProjectionParams.y - 1e-4 ) );
		float4 clipPosAdjusted = mul( UNITY_MATRIX_P, float4( viewPosAdjusted, 1 ) );
		clipPos.z = ( clipPosAdjusted.z / clipPosAdjusted.w ) * clipPos.w;
	}
#endif
	return clipPos;
}

inline void OctaImpostorVertex( inout float3 vertex, out float3 normal, out float4 tangent, out float4 uvsFrame1, out float4 uvsFrame2, out float4 uvsFrame3, out float4 octaFrame, out float4 viewPos )
{
	// Inputs
	float2 uvOffset = _AI_SizeOffset.zw;
	float parallax = -_Parallax; // check sign later
	float UVscale = _ImpostorSize;
	float framesXY = _Frames;
	float prevFrame = framesXY - 1;
	float3 fractions = 1.0 / float3( framesXY, prevFrame, UVscale );
	float fractionsFrame = fractions.x;
	float fractionsPrevFrame = fractions.y;
	float fractionsUVscale = fractions.z;
	float3 worldCameraPos;

#if ( defined( SHADERPASS ) && ( defined( SHADERPASS_DEPTHNORMALSONLY ) && SHADERPASS == SHADERPASS_DEPTHNORMALSONLY ) ) || defined( UNITY_PASS_SHADOWCASTER )
	float3 worldOrigin = 0;
	float4 perspective = float4( 0, 0, 0, 1 );
	if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
	{
		perspective = float4( 0, 0, 5000, 0 );
		worldOrigin = AI_ObjectToWorld._m03_m13_m23;
	}
	worldCameraPos = worldOrigin + mul( UNITY_MATRIX_I_V, perspective ).xyz;
#else
	// @diogo: not using UNITY_MATRIX_I_V here due to a unity bug sending slightly different matrices between depth-only and forward passes.
	if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
	{
		worldCameraPos = AI_ObjectToWorld._m03_m13_m23 + UNITY_MATRIX_I_V._m02_m12_m22 * 5000;
	}
	else
	{
	#if defined( AI_RENDERPIPELINE )
		worldCameraPos = GetCameraRelativePositionWS( _WorldSpaceCameraPos );
	#else
		#if defined( SCENEPICKINGPASS ) || defined( SCENESELECTIONPASS )
			worldCameraPos = UNITY_MATRIX_I_V._m03_m13_m23;
		#else
			worldCameraPos = _WorldSpaceCameraPos;
		#endif
	#endif
	}
#endif

	float3 objectCameraPosition = mul( AI_WorldToObject, float4( worldCameraPos, 1 ) ).xyz - _Offset.xyz; //ray origin
	float3 objectCameraDirection = normalize( objectCameraPosition );

	// Create orthogonal vectors to define the billboard
	float3 upVector = float3( 0, 1, 0 );
	float3 objectHorizontalVector = normalize( cross( objectCameraDirection, upVector ) );
	float3 objectVerticalVector = cross( objectHorizontalVector, objectCameraDirection );

	// Billboard
	float2 uvExpansion = vertex.xy;
	float3 billboard = objectHorizontalVector * uvExpansion.x + objectVerticalVector * uvExpansion.y;

	float3 objectDir = billboard - objectCameraPosition; // ray direction

	// @diogo: quantize to avoid a compiler bug causing mismatching values between passes
	objectCameraDirection = trunc( objectCameraDirection * 65536.0 ) / 65536.0;

	// Octahedron Frame
#if defined( _HEMI_ON )
	objectCameraDirection.y = max( 0.001, objectCameraDirection.y );
	float2 frameOcta = VectorToHemiOctahedron( objectCameraDirection.xzy ) * 0.5 + 0.5;
#else
	float2 frameOcta = VectorToOctahedron( objectCameraDirection.xzy ) * 0.5 + 0.5;
#endif

	// Setup for octahedron
	float2 prevOctaFrame = frameOcta * prevFrame;
	float2 baseOctaFrame = floor( prevOctaFrame );
	float2 fractionOctaFrame = ( baseOctaFrame * fractionsFrame );

	// Octa 1
	float2 octaFrame1 = ( baseOctaFrame * fractionsPrevFrame ) * 2.0 - 1.0;
#if defined( _HEMI_ON )
	float3 octa1WorldY = HemiOctahedronToVector( octaFrame1 ).xzy;
#else
	float3 octa1WorldY = OctahedronToVector( octaFrame1 ).xzy;
#endif

	float3 octa1LocalY;
	float2 uvFrame1;
	RayPlaneIntersectionUV( octa1WorldY, objectCameraPosition, objectDir, /*out*/ uvFrame1, /*out*/ octa1LocalY );

	float2 uvParallax1 = octa1LocalY.xy * fractionsFrame * parallax;
	uvFrame1 = ( uvFrame1 * fractionsUVscale + 0.5 ) * fractionsFrame + fractionOctaFrame;
	uvsFrame1 = float4( uvParallax1, uvFrame1 ) - float4( 0, 0, uvOffset );

	// Octa 2
	float2 fractPrevOctaFrame = frac( prevOctaFrame );
	float2 cornerDifference = lerp( float2( 0, 1 ) , float2( 1,0 ) , saturate( ceil( ( fractPrevOctaFrame.x - fractPrevOctaFrame.y ) ) ) );
	float2 octaFrame2 = ( ( baseOctaFrame + cornerDifference ) * fractionsPrevFrame ) * 2.0 - 1.0;
#if defined( _HEMI_ON )
	float3 octa2WorldY = HemiOctahedronToVector( octaFrame2 ).xzy;
#else
	float3 octa2WorldY = OctahedronToVector( octaFrame2 ).xzy;
#endif

	float3 octa2LocalY;
	float2 uvFrame2;
	RayPlaneIntersectionUV( octa2WorldY, objectCameraPosition, objectDir, /*out*/ uvFrame2, /*out*/ octa2LocalY );

	float2 uvParallax2 = octa2LocalY.xy * fractionsFrame * parallax;
	uvFrame2 = ( uvFrame2 * fractionsUVscale + 0.5 ) * fractionsFrame + ( ( cornerDifference * fractionsFrame ) + fractionOctaFrame );
	uvsFrame2 = float4( uvParallax2, uvFrame2 ) - float4( 0, 0, uvOffset );

	// Octa 3
	float2 octaFrame3 = ( ( baseOctaFrame + 1 ) * fractionsPrevFrame  ) * 2.0 - 1.0;
	#ifdef _HEMI_ON
		float3 octa3WorldY = HemiOctahedronToVector( octaFrame3 ).xzy;
	#else
		float3 octa3WorldY = OctahedronToVector( octaFrame3 ).xzy;
	#endif

	float3 octa3LocalY;
	float2 uvFrame3;
	RayPlaneIntersectionUV( octa3WorldY, objectCameraPosition, objectDir, /*out*/ uvFrame3, /*out*/ octa3LocalY );

	float2 uvParallax3 = octa3LocalY.xy * fractionsFrame * parallax;
	uvFrame3 = ( uvFrame3 * fractionsUVscale + 0.5 ) * fractionsFrame + ( fractionOctaFrame + fractionsFrame );
	uvsFrame3 = float4( uvParallax3, uvFrame3 ) - float4( 0, 0, uvOffset );

	// Maybe remove this?
	octaFrame = 0;
	octaFrame.xy = prevOctaFrame;
#if defined( AI_CLIP_NEIGHBOURS_FRAMES )
	octaFrame.zw = fractionOctaFrame;
#endif

	vertex = billboard + _Offset.xyz;
	normal = objectCameraDirection;
	tangent = float4( objectHorizontalVector, 1 );

#if defined( AI_RENDERPIPELINE )
	viewPos.xyz = TransformWorldToView( TransformObjectToWorld( vertex.xyz ) );
#else
	viewPos.xyz = UnityObjectToViewPos( vertex.xyz );
#endif

#if defined( EFFECT_HUE_VARIATION )
	float hueVariationAmount = frac( AI_ObjectToWorld[ 0 ].w + AI_ObjectToWorld[ 1 ].w + AI_ObjectToWorld[ 2 ].w );
	viewPos.w = saturate( hueVariationAmount * _HueVariation.a );
#else
	viewPos.w = 0;
#endif
}

inline void OctaImpostorVertex( inout float4 vertex, out float3 normal, out float4 tangent, out float4 uvsFrame1, out float4 uvsFrame2, out float4 uvsFrame3, out float4 octaFrame, out float4 viewPos )
{
	OctaImpostorVertex( vertex.xyz, normal, tangent, uvsFrame1, uvsFrame2, uvsFrame3, octaFrame, viewPos );
}

inline void OctaImpostorVertex( inout float4 vertex, out float3 normal, out float4 uvsFrame1, out float4 uvsFrame2, out float4 uvsFrame3, out float4 octaFrame, out float4 viewPos )
{
	float4 tangent;
	OctaImpostorVertex( vertex.xyz, normal, tangent, uvsFrame1, uvsFrame2, uvsFrame3, octaFrame, viewPos );
}

inline void OctaImpostorFragment( inout SurfaceOutputStandardSpecular o, out float4 clipPos, out float3 worldPos, float4 uvsFrame1, float4 uvsFrame2, float4 uvsFrame3, float4 octaFrame, float4 viewPos )
{
	// Weights
	float2 fraction = frac( octaFrame.xy );
	float2 invFraction = 1 - fraction;
	float3 weights;
	weights.x = min( invFraction.x, invFraction.y );
	weights.y = abs( fraction.x - fraction.y );
	weights.z = min( fraction.x, fraction.y );

	float parallax1 = tex2Dbias( _Normals, float4( uvsFrame1.zw, 0, -1 ) ).a;
	float parallax2 = tex2Dbias( _Normals, float4( uvsFrame2.zw, 0, -1 ) ).a;
	float parallax3 = tex2Dbias( _Normals, float4( uvsFrame3.zw, 0, -1 ) ).a;

	float2 parallax1_uv = ( ( 0.5 - parallax1 ) * uvsFrame1.xy ) + uvsFrame1.zw;
	float2 parallax2_uv = ( ( 0.5 - parallax2 ) * uvsFrame2.xy ) + uvsFrame2.zw;
	float2 parallax3_uv = ( ( 0.5 - parallax3 ) * uvsFrame3.xy ) + uvsFrame3.zw;

	// albedo alpha
	float4 albedo1 = tex2Dbias( _Albedo, float4( parallax1_uv, 0, _TextureBias ) );
	float4 albedo2 = tex2Dbias( _Albedo, float4( parallax2_uv, 0, _TextureBias ) );
	float4 albedo3 = tex2Dbias( _Albedo, float4( parallax3_uv, 0, _TextureBias ) );
	float4 blendedAlbedo = albedo1 * weights.x + albedo2 * weights.y + albedo3 * weights.z;

	// Early clip
	o.Alpha = blendedAlbedo.a;
#if !defined( AI_SKIP_ALPHA_CLIP )
	clip( o.Alpha - _ClipMask );
#endif

#if defined( AI_CLIP_NEIGHBOURS_FRAMES )
	float t = ceil( fraction.x - fraction.y );
	float4 cornerDifference = float4( t, 1 - t, 1, 1 );

	float2 step_1 = ( parallax1_uv - octaFrame.zw ) * _Frames;
	float4 step23 = ( float4( parallax2_uv, parallax3_uv ) -  octaFrame.zwzw ) * _Frames - cornerDifference;

	step_1 = step_1 * ( 1 - step_1 );
	step23 = step23 * ( 1 - step23 );

	float3 steps;
	steps.x = step_1.x * step_1.y;
	steps.y = step23.x * step23.y;
	steps.z = step23.z * step23.w;
	steps = step( -steps, 0 );

	float final = dot( steps, weights );

	clip( final - 0.5 );
#endif

#if defined( EFFECT_HUE_VARIATION )
	half3 shiftedColor = lerp( blendedAlbedo.rgb, _HueVariation.rgb, viewPos.w );
	half maxBase = max( blendedAlbedo.r, max( blendedAlbedo.g, blendedAlbedo.b ) );
	half newMaxBase = max( shiftedColor.r, max( shiftedColor.g, shiftedColor.b ) );
	maxBase /= newMaxBase;
	maxBase = maxBase * 0.5f + 0.5f;
	shiftedColor.rgb *= maxBase;
	blendedAlbedo.rgb = saturate( shiftedColor );
#endif
	o.Albedo = blendedAlbedo.rgb;

	// Normal
	float3 normal1 = tex2Dbias( _Normals, float4( parallax1_uv, 0, _TextureBias ) ).rgb;
	float3 normal2 = tex2Dbias( _Normals, float4( parallax2_uv, 0, _TextureBias ) ).rgb;
	float3 normal3 = tex2Dbias( _Normals, float4( parallax3_uv, 0, _TextureBias ) ).rgb;
	float3 blendedNormal = normal1 * weights.x  + normal2 * weights.y + normal3 * weights.z;

	float3 objectNormal = blendedNormal * 2.0 - 1.0;
	float3 worldNormal = normalize( mul( (float3x3)AI_ObjectToWorld, objectNormal ) );
	o.Normal = worldNormal;

	// Depth
	const float objectScale = length( AI_ObjectToWorld[ 2 ].xyz );
	float depth = parallax1 * weights.x + parallax2 * weights.y + parallax3 * weights.z;
	depth = ( depth - 0.5 ) * _DepthSize * objectScale;

	// Specular Smoothness
#if defined( _SPECULARMAP )
	float4 specular1 = AI_SAMPLEBIAS( _Specular, sampler_Specular, parallax1_uv, _TextureBias );
	float4 specular2 = AI_SAMPLEBIAS( _Specular, sampler_Specular, parallax2_uv, _TextureBias );
	float4 specular3 = AI_SAMPLEBIAS( _Specular, sampler_Specular, parallax3_uv, _TextureBias );
	float4 blendedSpecular = specular1 * weights.x  + specular2 * weights.y + specular3 * weights.z;
	o.Specular = blendedSpecular.rgb;
	o.Smoothness = blendedSpecular.a;
#else
	o.Specular = 0;
	o.Smoothness = 0;
#endif

#if defined( _OCCLUSIONMAP )
	float occlusion1 = tex2Dbias( _Occlusion, float4( parallax1_uv, 0, _TextureBias ) ).g;
	float occlusion2 = tex2Dbias( _Occlusion, float4( parallax2_uv, 0, _TextureBias ) ).g;
	float occlusion3 = tex2Dbias( _Occlusion, float4( parallax3_uv, 0, _TextureBias ) ).g;
	o.Occlusion = occlusion1 * weights.x  + occlusion2 * weights.y + occlusion3 * weights.z;
#else
	o.Occlusion = 1;
#endif

#if defined( _EMISSIONMAP )
	// Emission Occlusion
	float3 emission1 = tex2Dbias( _Emission, float4( parallax1_uv, 0, _TextureBias ) ).rgb;
	float3 emission2 = tex2Dbias( _Emission, float4( parallax2_uv, 0, _TextureBias ) ).rgb;
	float3 emission3 = tex2Dbias( _Emission, float4( parallax3_uv, 0, _TextureBias ) ).rgb;
	o.Emission = emission1 * weights.x  + emission2 * weights.y + emission3 * weights.z;
#else
	o.Emission = 0;
#endif

#if defined( _POSITIONMAP )
	// Position
	float4 position1 = tex2Dbias( _Position, float4( parallax1_uv, 0, _TextureBias ) );
	float4 position2 = tex2Dbias( _Position, float4( parallax2_uv, 0, _TextureBias ) );
	float4 position3 = tex2Dbias( _Position, float4( parallax3_uv, 0, _TextureBias ) );
	float4 blendedPosition = position1 * weights.x  + position2 * weights.y + position3 * weights.z;

	float3 objectPosition = blendedPosition.xyz * _AI_BoundsSize + _AI_BoundsMin;
	float3 worldPosition = mul( AI_ObjectToWorld, float4( objectPosition, 1 ) ).xyz;

	if ( blendedPosition.a > 0 )
	{
		viewPos.xyz = mul( UNITY_MATRIX_V, float4( worldPosition.xyz, 1 ) ).xyz;
		depth = 0;
	}
#endif

	float3 viewDir = ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 ) ? float3( 0, 0, 1 ) : normalize( -viewPos.xyz );
#if defined( AI_RENDERPIPELINE )
	#if ( defined( SHADERPASS ) && ( ( defined( SHADERPASS_SHADOWS ) && SHADERPASS == SHADERPASS_SHADOWS ) || ( defined( SHADERPASS_SHADOWCASTER ) && SHADERPASS == SHADERPASS_SHADOWCASTER ) ) ) || defined( UNITY_PASS_SHADOWCASTER )
		viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );
	#else
		viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );
	#endif
#else
	#if defined( SHADOWS_DEPTH ) && !defined( SPOT ) && !defined( CUBE ) && !defined( DIRECTIONAL )
		if ( any( unity_LightShadowBias ) ) // get only the shadowcaster, this is a hack
		{
			viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );
		}
		else
		{
			viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );
		}
	#elif defined( UNITY_PASS_SHADOWCASTER )
		viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );
	#else
		viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );
	#endif
#endif

	worldPos = mul( UNITY_MATRIX_I_V, float4( viewPos.xyz, 1 ) ).xyz;
	clipPos = mul( UNITY_MATRIX_P, float4( viewPos.xyz, 1 ) );

#if defined( AI_RENDERPIPELINE )
	#if defined( UNITY_PASS_SHADOWCASTER ) && !defined( SHADERPASS )
		#if defined( UNITY_REVERSED_Z )
			clipPos.z = min( clipPos.z, clipPos.w * UNITY_NEAR_CLIP_VALUE );
		#else
			clipPos.z = max( clipPos.z, clipPos.w * UNITY_NEAR_CLIP_VALUE );
		#endif
	#endif
#else
	#if defined( SHADOWS_DEPTH ) && !defined( SPOT ) && !defined( CUBE ) && !defined( DIRECTIONAL )
		clipPos = UnityApplyLinearShadowBias( clipPos );
	#endif
#endif

	clipPos.xyz /= clipPos.w;

	if ( UNITY_NEAR_CLIP_VALUE < 0 )
	{
		clipPos = clipPos * 0.5 + 0.5;
	}
}

inline void SphereImpostorVertex( inout float3 vertex, out float3 normal, out float4 tangent, out float4 frameUVs, out float4 viewPos )
{
	// INPUTS
	float2 uvOffset = _AI_SizeOffset.zw;
	float sizeX = _FramesX;
	float sizeY = _FramesY - 1; // adjusted
	float UVscale = _ImpostorSize;
	float4 fractions = 1 / float4( sizeX, _FramesY, sizeY, UVscale );
	float2 sizeFraction = fractions.xy;
	float axisSizeFraction = fractions.z;
	float fractionsUVscale = fractions.w;
	float3 worldCameraPos;

#if ( defined(SHADERPASS) && (defined(SHADERPASS_DEPTHNORMALSONLY) && SHADERPASS == SHADERPASS_DEPTHNORMALSONLY) ) || defined(UNITY_PASS_SHADOWCASTER)
	float3 worldOrigin = 0;
	float4 perspective = float4( 0, 0, 0, 1 );
	if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
	{
		perspective = float4( 0, 0, 5000, 0 );
		worldOrigin = AI_ObjectToWorld._m03_m13_m23;
	}
	worldCameraPos = worldOrigin + mul( UNITY_MATRIX_I_V, perspective ).xyz;
#else
	// @diogo: not using UNITY_MATRIX_I_V here due to a unity bug sending slightly different matrices between depth-only and forward passes.
	if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
	{
		worldCameraPos = AI_ObjectToWorld._m03_m13_m23 + UNITY_MATRIX_I_V._m02_m12_m22 * 5000;
	}
	else
	{
	#if defined( AI_RENDERPIPELINE )
		worldCameraPos = GetCameraRelativePositionWS( _WorldSpaceCameraPos );
	#else
		worldCameraPos = _WorldSpaceCameraPos;
	#endif
	}
#endif

	float3 objectCameraPosition = mul( AI_WorldToObject, float4( worldCameraPos, 1 ) ).xyz - _Offset.xyz; //ray origin
	float3 objectCameraDirection = normalize( objectCameraPosition );

	// Create orthogonal vectors to define the billboard
	float3 upVector = float3( 0,1,0 );
	float3 objectHorizontalVector = normalize( cross( objectCameraDirection, upVector ) );
	float3 objectVerticalVector = cross( objectHorizontalVector, objectCameraDirection );

	// Create vertical radial angle
	float verticalAngle = frac( atan2( -objectCameraDirection.z, -objectCameraDirection.x ) * AI_INV_TWO_PI ) * sizeX + 0.5;

	// Create horizontal radial angle
	float verticalDot = dot( objectCameraDirection, upVector );
	float upAngle = ( acos( -verticalDot ) * AI_INV_PI ) + axisSizeFraction * 0.5f;
	float yRot = sizeFraction.x * AI_PI * verticalDot * ( 2 * frac( verticalAngle ) - 1 );

	// Billboard rotation
	float2 uvExpansion = vertex.xy;
	float cosY = cos( yRot );
	float sinY = sin( yRot );
	float2 uvRotator = mul( uvExpansion, float2x2( cosY, -sinY, sinY, cosY ) );

	// Billboard
	float3 billboard = objectHorizontalVector * uvRotator.x + objectVerticalVector * uvRotator.y;

	// Frame coords
	float2 relativeCoords = float2( floor( verticalAngle ), min( floor( upAngle * sizeY ), sizeY ) );
	float2 frameUV = ( ( uvExpansion * fractionsUVscale + 0.5 ) + relativeCoords ) * sizeFraction;

	frameUVs.xy = frameUV - uvOffset;

	// Parallax
#if defined( _USE_PARALLAX_ON )
	float3 objectNormalVector = cross( objectHorizontalVector, -objectVerticalVector );
	float3x3 worldToLocal = float3x3( objectHorizontalVector, objectVerticalVector, objectNormalVector );
	float3 sphereLocal = normalize( mul( worldToLocal, billboard - objectCameraPosition ) );
	frameUVs.zw = sphereLocal.xy * sizeFraction * _Parallax;
#else
	frameUVs.zw = 0;
#endif

	vertex = billboard + _Offset.xyz;
	normal = objectCameraDirection;
	tangent = float4( objectHorizontalVector, 1 );

#if defined( AI_RENDERPIPELINE )
	viewPos.xyz = TransformWorldToView( TransformObjectToWorld( vertex.xyz ) );
#else
	viewPos.xyz = UnityObjectToViewPos( vertex.xyz );
#endif

#if defined( EFFECT_HUE_VARIATION )
	float hueVariationAmount = frac( AI_ObjectToWorld[ 0 ].w + AI_ObjectToWorld[ 1 ].w + AI_ObjectToWorld[ 2 ].w );
	viewPos.w = saturate( hueVariationAmount * _HueVariation.a );
#else
	viewPos.w = 0;
#endif
}

inline void SphereImpostorVertex( inout float4 vertex, out float3 normal, out float4 tangent, out float4 frameUVs, out float4 viewPos )
{
	SphereImpostorVertex( vertex.xyz, normal, tangent, frameUVs, viewPos );
}

inline void SphereImpostorVertex( inout float4 vertex, out float3 normal, out float4 frameUVs, out float4 viewPos )
{
	float4 tangent;
	SphereImpostorVertex( vertex.xyz, normal, tangent, frameUVs, viewPos );
}

inline void SphereImpostorFragment( inout SurfaceOutputStandardSpecular o, out float4 clipPos, out float3 worldPos, float4 frameUV, float4 viewPos )
{
#if defined( _USE_PARALLAX_ON )
	float parallax = tex2Dbias( _Normals, float4( frameUV.xy, 0, -1 ) ).a;
	frameUV.xy = ( ( 0.5 - parallax ) * frameUV.zw ) + frameUV.xy;
#endif

	// Albedo + Alpha
	float4 albedo = tex2Dbias( _Albedo, float4( frameUV.xy, 0, _TextureBias ) );

	// Early clip
	o.Alpha = albedo.a;
#if !defined( AI_SKIP_ALPHA_CLIP )
	clip( o.Alpha - _ClipMask );
#endif

#if defined( EFFECT_HUE_VARIATION )
	half3 shiftedColor = lerp( albedo.rgb, _HueVariation.rgb, viewPos.w );
	half maxBase = max( albedo.r, max( albedo.g, albedo.b ) );
	half newMaxBase = max( shiftedColor.r, max( shiftedColor.g, shiftedColor.b ) );
	maxBase /= newMaxBase;
	maxBase = maxBase * 0.5f + 0.5f;
	shiftedColor.rgb *= maxBase;
	albedo.rgb = saturate( shiftedColor );
#endif
	o.Albedo = albedo.rgb;

	// Normal
	float4 normalSample = tex2Dbias( _Normals, float4( frameUV.xy, 0, _TextureBias ) );
	float4 objectNormal = normalSample * 2 - 1;
	float3 worldNormal = normalize( mul( ( float3x3 )AI_ObjectToWorld, objectNormal.xyz ) );
	o.Normal = worldNormal;

	// Depth
	const float objectScale = length( AI_ObjectToWorld[ 2 ].xyz );
	float depth = ( normalSample.a - 0.5 ) * _DepthSize * objectScale;

	// Specular Smoothness
#if defined( _SPECULARMAP )
	float4 specular = AI_SAMPLEBIAS( _Specular, sampler_Specular, frameUV.xy, _TextureBias );
	o.Specular = specular.rgb;
	o.Smoothness = specular.a;
#else
	o.Specular = 0;
	o.Smoothness = 0;
#endif

	// Occlusion
#if defined( _OCCLUSIONMAP )
	o.Occlusion = tex2Dbias( _Occlusion, float4( frameUV.xy, 0, _TextureBias ) ).g;
#else
	o.Occlusion = 1;
#endif

	// Emission
#if defined( _EMISSIONMAP )
	o.Emission = tex2Dbias( _Emission, float4( frameUV.xy, 0, _TextureBias ) ).rgb;
#else
	o.Emission = 0;
#endif

#if defined( _POSITIONMAP )
	// Position
	float4 position = tex2Dbias( _Position, float4( frameUV.xy, 0, _TextureBias ) );
	float3 objectPosition = position.xyz * _AI_BoundsSize + _AI_BoundsMin;
	float3 worldPosition = mul( AI_ObjectToWorld, float4( objectPosition, 1 ) ).xyz;

	if ( position.a > 0 )
	{
		viewPos.xyz = mul( UNITY_MATRIX_V, float4( worldPosition.xyz, 1 ) ).xyz;
		depth = 0;
	}
#endif

	float3 viewDir = ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 ) ? float3( 0, 0, 1 ) : normalize( -viewPos.xyz );
#if defined( AI_RENDERPIPELINE )
	#if ( defined( SHADERPASS ) && ( ( defined( SHADERPASS_SHADOWS ) && SHADERPASS == SHADERPASS_SHADOWS ) || ( defined( SHADERPASS_SHADOWCASTER ) && SHADERPASS == SHADERPASS_SHADOWCASTER ) ) ) || defined( UNITY_PASS_SHADOWCASTER )
		viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );
	#else
		viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );
	#endif
#else
	#if defined( SHADOWS_DEPTH ) && !defined( SPOT ) && !defined( CUBE ) && !defined( DIRECTIONAL )
		if ( any( unity_LightShadowBias ) ) // get only the shadowcaster, this is a hack
		{
			viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );
		}
		else
		{
			viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );
		}
	#elif defined( UNITY_PASS_SHADOWCASTER )
		viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );
	#else
		viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );
	#endif
#endif

	worldPos = mul( UNITY_MATRIX_I_V, float4( viewPos.xyz, 1 ) ).xyz;
	clipPos = mul( UNITY_MATRIX_P, float4( viewPos.xyz, 1 ) );

#if defined( AI_RENDERPIPELINE )
	#if defined( UNITY_PASS_SHADOWCASTER ) && !defined( SHADERPASS )
		#if defined( UNITY_REVERSED_Z )
			clipPos.z = min( clipPos.z, clipPos.w * UNITY_NEAR_CLIP_VALUE );
		#else
			clipPos.z = max( clipPos.z, clipPos.w * UNITY_NEAR_CLIP_VALUE );
		#endif
	#endif
#else
	#if defined( SHADOWS_DEPTH ) && !defined( SPOT ) && !defined( CUBE ) && !defined( DIRECTIONAL )
		clipPos = UnityApplyLinearShadowBias( clipPos );
	#endif
#endif

	clipPos.xyz /= clipPos.w;

	if ( UNITY_NEAR_CLIP_VALUE < 0 )
	{
		clipPos = clipPos * 0.5 + 0.5;
	}
}
#endif //AMPLIFYIMPOSTORS_INCLUDED
