/*Adapted from CPU Gems 3, Chapter 27
https://developer.nvidia.com/gpugems/GPUGems3/gpugems3_ch27.html
*/

float4x4 WorldViewProjection;
float4x4 InvWorldViewProjection;

float4x4 preWorldViewProjection;
float4x4 preInvWorldViewProjection;

float4x4 WorldInverseTranspose;

float NumSamples = 8;

float isZoom = 0.0f;

texture DepthMap;
sampler DepthMapSampler = sampler_state
{
	Texture = <DepthMap>;
	MinFilter = POINT;
	MagFilter = POINT;
	MipFilter = POINT;
	AddressU = CLAMP;
	AddressV = CLAMP;
	AddressW = CLAMP;
};

texture SceneTexture;
sampler SceneSampler = sampler_state
{
	Texture = <SceneTexture>;
	MinFilter = POINT;
	MagFilter = POINT;
	MipFilter = POINT;
	AddressU = CLAMP;
	AddressV = CLAMP;
	AddressW = CLAMP;
};


struct VertexShaderInput
{
	float4 Position : POSITION0;
};

struct VertexShaderOutput
{
	float4 Position : POSITION0;
	float4 Position2D : TEXCOORD0;
};

VertexShaderOutput DepthMapVertexShader(VertexShaderInput input)
{
	VertexShaderOutput output;
	output.Position = mul(input.Position, WorldViewProjection);
	output.Position2D = output.Position;
	return output;
}
float4 DepthMapPixelShader(VertexShaderOutput input) : COLOR0
{
	float4 projTexCoord = input.Position.z / input.Position.w;
	projTexCoord.xy = 0.5f * projTexCoord.xy + float2(0.5f, 0.5f);
	projTexCoord.y = 1.0f - projTexCoord.y;
	float depth = 1.0f - projTexCoord;
	float4 color = (depth>0) ? depth : 0;
	return float4(color.r,0,0,1);
}

struct ppVertexShaderOutput {
	float2 UV0 : TEXCOORD0;

};

ppVertexShaderOutput vsScreenUV(float4 inPos : POSITION, float2 inTex : TEXCOORD0) {
	ppVertexShaderOutput output;
	output.UV0 = inTex;
	return output;
}

float4 BlurredScenePixelShader(float4 position : SV_Position, float4 colorIn : COLOR0, float2 texCoordIn : TEXCOORD0) : COLOR0{
	float2 texCoord = texCoordIn;

	// Get the depth buffer value at this pixel.  
	float zOverW = tex2D(DepthMapSampler, texCoord).r;
	// H is the viewport position at this pixel in the range -1 to 1.  
	float4 H = float4(texCoord.x * 2 - 1, (1 - texCoord.y) * 2 - 1, (1-zOverW)*2-1, 1);
	// Transform by the view-projection inverse.  
	float4 D = mul(H, InvWorldViewProjection);
	// Divide by w to get the world position.  
	float4 worldPos = D / D.w;

	// Current viewport position  
	float4 currentPos = H;
	// Use the world position, and transform by the previous view-  
	// projection matrix.  
	float4 previousPos = mul(worldPos, preWorldViewProjection);
	// Convert to nonhomogeneous points [-1,1] by dividing by w.  
	previousPos /= previousPos.w;
	// Use this frame's position and last frame's to compute the pixel  
	// velocity.  
	float2 velocity = ((currentPos.xy - previousPos.xy))/2.0f;
	velocity.y *= -1; //flip y (NDC y would be opposite)
	//Need to divide by a number, that also depends on the scene, to get samples around the texture
	if ( isZoom > 0 )
		velocity.xy /= (2.0f * NumSamples * NumSamples); //for zoom
	else
		velocity.xy /= (NumSamples * 1500.0f); //for rotation, pan

	// Get the initial color at this pixel.  
	float3 color = tex2D(SceneSampler, texCoord.xy).rgb;
	texCoord += velocity;
	for (int i = 1; i < NumSamples; ++i, texCoord += velocity)
	{
		// Sample the color buffer along the velocity vector.  
		float3 currentColor = tex2D(SceneSampler, texCoord.xy).rgb;
		// Add the current color to our color sum.  
		color += currentColor;
	}
	// Average all of the samples to get the final blur color.  
	float3 finalColor = color.rgb / (NumSamples);
	return saturate(float4(finalColor, 1.0f));
}


technique DepthMapTechnique
{
	pass Pass1
	{
		VertexShader = compile vs_4_0 DepthMapVertexShader();
		PixelShader = compile ps_4_0 DepthMapPixelShader();
	}
}

technique BlurredScenePostProcessTechnique
{
	pass Pass1
	{
		PixelShader = compile ps_4_0 BlurredScenePixelShader();
	}
}