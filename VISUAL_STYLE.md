# Visual style: first pass

The cabin combines the practical light surfaces and labeling discipline of ISS hardware
with the simple shapes and strong screen contrast of Carrier Command 2. The exterior
uses the same restrained colors and broad light/shadow shapes.

## Color roles

| Role | First-pass color | Use |
| --- | --- | --- |
| Shell | `#C8CBC4` | Main cabin structure |
| Panel | `#858F8C` | Replaceable instrument plates |
| Recess | `#303D41` | Seats, monitor surrounds, deep gaps |
| Grip | `#4A585B` | Levers, knobs, switches |
| Interaction | `#638F9B` | Buttons and touch points |
| Active indicator | `#6AD2AA` | Ordinary enabled state |
| Red | Reserved | Emergency or alarm |
| Yellow | Reserved | Caution and warning |

The hex values are game choices, not official ISS paint specifications. The ESA ISS
user guide describes white, off-white, medium grey, black and tan hardware finishes;
it reserves red labels for emergency use and yellow labels for caution and warning.
Source: <https://wsn.spaceflight.esa.int/docs/EUG2LGPr3/EUG2LGPr3-7-ISS.pdf>, §7.4.3.

## Materials and lighting

- `Suturn/Stylized Matte`: two broad light bands, no specular highlights. Used for cabin hardware.
- `Suturn/Stylized Body`: a distinct day/night edge, optional latitude bands. Used by the icy moons.
- `Suturn/Saturn`: flat-stepped latitude belts with longitude-warped edges, band streaks,
  a grey-blue polar cap and the north polar hexagon. Hard light zones, ring shadows on the
  planet, and a thin haze limb. Like Titan, the sphere is ray-cast on an inflated proxy mesh.
- `Suturn/Stylized Ring`: separate transparent material for every named ring.
- `Suturn/Space Sky`: skybox of the exterior camera. Procedural stars are fixed in the
  simulation's inertial axes and drawn as crisp dots sized in screen pixels. The Sun disc
  has its true angular size (about a pixel from Saturn) with a minimum on-screen radius,
  plus two flat glare rings instead of a gradient. `ExteriorView` supplies the directions.
  The sky is drawn by a separate camera below the exterior one: a skybox pass after the
  bodies would overwrite them, because at this near/far range distant bodies are
  indistinguishable from the far plane in the depth buffer.
- Existing unlit screen and instrument-line shaders remain the navigation display language.

The preview in `Assets/Art/Previews/FirstPass.png` is rendered from `EyeCam` with a
temporary Saturn sample and temporary screen graphics. Those preview objects and graphics
are not saved into the game scene. Recreate it with **Cockpit → Style: capture material preview**.

## Saturn rings

Each ring is a separate child renderer with its own material, including the faint G ring.
The scene exposes A through G independently. Ring radii follow NASA NSSDCA's table:
<https://nssdc.gsfc.nasa.gov/planetary/factsheet/satringfact.html>. The F ring is drawn
400 km wide for visibility because that table provides its center radius only. E and G
are deliberately very faint. Their colors and opacities can be adjusted per material.

The rings lie in Saturn's equatorial plane, which is the simulation's reference plane for
orbits (XY, pole along Z). `ExteriorView` orients every body so its local Y is that pole,
so rings stay fixed against the stars while the ship turns. From Titan's orbit the rings
are therefore seen almost edge-on. The ring shader darkens the planet's shadow and the
face the Sun does not reach.

## Terraformed Titan

`Suturn/Titan Terraforming` draws Titan on one smooth sphere without texture maps.
Yellow haze covers olive-green terraformed ground; the palette is a flat take on a
banded, swirling gas-giant look.
The cloud field combines four scales of 3D gradient Perlin noise with Worley erosion
at thin margins. Latitude shear and three localized spherical vortices organize the
large weather systems. Noise is stretched along longitude and bent zonal belts are
added to the density, so systems read as bands. Smaller billows use a less distorted coordinate field, avoiding
uniformly stretched marble-like detail. All coordinates are object-space and continuous
across UV seams and poles. This is an artistic flow deformation, not a fluid simulation.

Density controls both the clear gaps and the stepped opacity of thin cloud veils.
Every color transition is a hard step antialiased over one pixel: a single half-opacity
band of thin cloud along margins, two ground tones, flat cirrus streaks, and one shadowed
billow tone inside the cloud body.
At the default density threshold of 0.42, a 12,000-point numerical sample of the sphere
had approximately 42% clear ground. The cloud body retains three light zones at
thresholds of 0.5 and -0.1. Density gradients add subtle directional shading within those
zones; there is no volume ray marching. The mesh is only an inflated proxy: the fragment
stage ray-casts the true sphere, so the disk stays round on a low-poly mesh. The pale haze
limb is drawn outside the disk and thins toward the night side, where only the directed
Saturn fill lights it.
`ExteriorView` supplies the direction to Saturn per Titan renderer.

Material controls: **Weather system scale**, **Longitude stretch**, **Zonal belts**, **Cloud density threshold**, **Cloud detail**,
**Thin cloud depth**, and the rim width. The shape is static in object space.

`Assets/Art/Previews/TitanFirstPass.png` shows the material from the pilot camera;
`TitanCloseup.png` shows the weather field at inspection scale; `TitanFarSide.png`
checks the opposite hemisphere. Use
**Cockpit → Style: capture Titan preview/closeup** to regenerate them.
