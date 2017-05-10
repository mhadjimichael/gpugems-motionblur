float4x4  World;
float4x4  View;
float4x4  Projection;
float4x4  WorldInverseTranspose;

//Light options
float4  AmbientColor;
float4  DiffuseColor;
float4	SpecularColor;
float	Shininess;
float	SpecularIntensity;
float	DiffuseIntensity;

float3 LightPosition;
float3 CameraPosition;

struct VertexShaderInput {
	float4 Position: POSITION;
	float4 Normal: NORMAL;
	float2 TexCoord : TEXCOORD0;
};

struct VertexShaderOutput {
	float4 Position: POSITION;
	float4 Color: COLOR;
	float4 Normal : TEXCOORD0;
	float4 WorldPosition : TEXCOORD1;
	float2 TexCoord : TEXCOORD2;
};

texture UVTexture;
sampler UVSampler = sampler_state
{
	Texture = <UVTexture>;
	MinFilter = LINEAR;
	MagFilter = LINEAR;
	MipFilter = LINEAR;
	AddressU = CLAMP;
	AddressV = CLAMP;
};

//Vertex shader
VertexShaderOutput PhongVertexShaderFunction(VertexShaderInput input) {
	VertexShaderOutput output;
	output.WorldPosition = mul(input.Position, World);
	output.Position = mul(mul(output.WorldPosition, View), Projection);
	output.Normal = mul(input.Normal, WorldInverseTranspose);
	output.Color = 0;
	output.TexCoord = input.TexCoord;
	return output;
}

//Pixel Shader
float4 PhongPixelShaderFunction(VertexShaderOutput input) : COLOR{
	float3 N = normalize(input.Normal.xyz);
	float3 V = normalize(CameraPosition - input.WorldPosition.xyz);
	float3 L = normalize(LightPosition);
	float3 R = reflect(-L, N);
	float facing = dot(N, L) > 0 ? 1 : 0;
	float4 diffuse = DiffuseIntensity * DiffuseColor * max(0, dot(N, L));
	float4 specular = SpecularIntensity * SpecularColor*max(0, dot(N, L))*facing;

	//do 50% color and 50% texture
	return lerp(tex2D(UVSampler, input.TexCoord),  (AmbientColor + diffuse*DiffuseColor + specular*SpecularColor), 0.5);
}

technique Phong
{
	pass Pass1
	{
		VertexShader = compile vs_4_0 PhongVertexShaderFunction();
		PixelShader = compile ps_4_0 PhongPixelShaderFunction();
	}
}