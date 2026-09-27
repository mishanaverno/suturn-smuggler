#ifndef SUTURN_ECLIPSE_INCLUDED
#define SUTURN_ECLIPSE_INCLUDED

// Тень от тел системы. Все крупные тела — шары, и тень от шара считается здесь же, без
// карты теней: карта не переходит между сценами разного масштаба, а это — просто геометрия.
//
// Тела передаёт ExteriorView раз в кадр: xyz — центр в метрах в мировых осях относительно
// корабля, w — радиус в метрах. Корабль стоит в нуле обеих внешних сцен, поэтому одних и тех
// же чисел хватает любой из них: ближний план уже в метрах, дальний — в своих единицах, и
// его точку переводит EclipseSunlightExterior.

#define ECLIPSE_MAX_BODIES 16

float4 _EclipseBodies[ECLIPSE_MAX_BODIES];
float _EclipseBodyCount;
float3 _EclipseSunDirection;
float _EclipseSunRadius;
float _EclipseExteriorScale;

// Доля солнечного диска, видимая из точки: 1 — свет, 0 — полная тень. Полутень — пока край
// тела проходит по диску, линейно по углу между их центрами. Угол — через atan2, а не acos:
// диск Солнца у Сатурна в полмиллирадиана, и acos во float его просто не различает.
float EclipseSunlight(float3 positionMeters)
{
    float lit = 1.0;
    for (int i = 0; i < (int)_EclipseBodyCount; i++)
    {
        float3 toBody = _EclipseBodies[i].xyz - positionMeters;
        float distance = length(toBody);
        float radius = _EclipseBodies[i].w;
        // Своё тело не затеняет само себя: ночную сторону шейдер тела и так рисует ночью, а
        // точка на поверхности без допуска то внутри шара, то снаружи — и у терминатора
        // появлялась бы полоса.
        if (distance <= radius * 1.001) continue;
        float3 direction = toBody / distance;
        float separation = atan2(length(cross(direction, _EclipseSunDirection)), dot(direction, _EclipseSunDirection));
        float bodyRadius = asin(radius / distance);
        lit = min(lit, saturate((separation - bodyRadius + _EclipseSunRadius) / (2.0 * _EclipseSunRadius)));
    }
    return lit;
}

// То же для точки дальнего плана, в его единицах.
float EclipseSunlightExterior(float3 worldPosition)
{
    return EclipseSunlight(worldPosition * _EclipseExteriorScale);
}

#endif
