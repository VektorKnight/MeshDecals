# Mesh Decals
 An example implementation of mesh-based decals in Unity.
 The decal mesh is generated by clipping the triangles of any intersecting meshes against the decal volume. The result is a decal mesh which in-theory is much cheaper to render than a decal projected at runtime. The mesh-based approach also allows decals to use simple materials and receive lighting without reliance on Gbuffers or reconstructed normals from depth.
 
 ![decal](https://user-images.githubusercontent.com/4340480/232976334-1b8e3400-19d3-4f06-b628-84ed2dba448b.png)
 
## Pros
- Decals become regular meshes utilizing simpler materials making them cheaper at runtime, especially on weaker hardware.
- Does not rely on GBuffers or depth to project on a surface and receive lighting both realtime and baked.
- Mesh representation allows for optimization such as a further clipping by a polygon hull of the image to reduce overdraw.
- Decals can be highly selective about which meshes they consider or exclude removing the need for stencil masking.
- Can project onto any mesh surface in the scene without extra work.

## Cons
- Requires integration into a build pipeline and additional editor tooling to facilitate this.
- Potential increase in draw calls as unique meshes cannot benefit from instancing.
- Package size and VRAM usage may increase due to each decal requiring a unique mesh.
 
## TODO
- Pack triangles from source meshes into sub-meshes on the decal mesh.
- Simplify decal mesh to remove co-planar triangles.
 
 ## Disclaimer
 This is in no-way a finished product and the goal was to explore the various algorithms involved. The code has been kept relatively simple with decent documentation to serve as an example should you wish to build a similar system for your own projects. While things are at a good stopping point, I may update this project to tackle some remaining TODOs such as mesh simplification and some potential optimizations.
