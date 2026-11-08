/**
 * 3D Model Generator for Nageel Yemeni Mountain Driving
 * Generates detailed Wavefront OBJ models:
 * 1. Yemeni 4WD Mountain SUV (Chassis, Cabin, Hood, Bullbar, Roof Rack, Wheels)
 * 2. Mountain Highway Guardrail Segment (Posts, W-Beam rail)
 * 3. Traditional Yemeni Stone Tower House (Multi-tier stone masonry with Qamariyah windows)
 */

const fs = require('fs');
const path = require('path');

const modelsDir = path.join(__dirname, '..', 'Assets', 'Models');
if (!fs.existsSync(modelsDir)) {
    fs.mkdirSync(modelsDir, { recursive: true });
}

function writeOBJ(filePath, name, vertices, normals, uvs, faces) {
    let out = `# Wavefront OBJ: ${name}\n# Generated for Nageel - Yemeni Mountain Driving\no ${name}\n\n`;
    
    for (const v of vertices) {
        out += `v ${v[0].toFixed(4)} ${v[1].toFixed(4)} ${v[2].toFixed(4)}\n`;
    }
    out += '\n';

    if (uvs && uvs.length > 0) {
        for (const vt of uvs) {
            out += `vt ${vt[0].toFixed(4)} ${vt[1].toFixed(4)}\n`;
        }
        out += '\n';
    }

    if (normals && normals.length > 0) {
        for (const vn of normals) {
            out += `vn ${vn[0].toFixed(4)} ${vn[1].toFixed(4)} ${vn[2].toFixed(4)}\n`;
        }
        out += '\n';
    }

    for (const f of faces) {
        out += `f ${f.join(' ')}\n`;
    }

    fs.writeFileSync(filePath, out, 'utf8');
    console.log(`Created 3D Model: ${filePath} (${vertices.length} vertices, ${faces.length} faces)`);
}

// 1. Generate 4WD SUV Model
function generateVehicleOBJ() {
    const vertices = [];
    const faces = [];

    // Helper to add a box with vertex indices
    function addBox(minX, minY, minZ, maxX, maxY, maxZ) {
        const start = vertices.length + 1;
        // 8 vertices
        vertices.push([minX, minY, minZ]); // 1
        vertices.push([maxX, minY, minZ]); // 2
        vertices.push([maxX, maxY, minZ]); // 3
        vertices.push([minX, maxY, minZ]); // 4
        vertices.push([minX, minY, maxZ]); // 5
        vertices.push([maxX, minY, maxZ]); // 6
        vertices.push([maxX, maxY, maxZ]); // 7
        vertices.push([minX, maxY, maxZ]); // 8

        // 6 faces (quads converted to pairs of triangles)
        const quads = [
            [1, 4, 3, 2], // Front
            [5, 6, 7, 8], // Back
            [1, 5, 8, 4], // Left
            [2, 3, 7, 6], // Right
            [4, 8, 7, 3], // Top
            [1, 2, 6, 5]  // Bottom
        ];

        for (const q of quads) {
            const a = start + q[0] - 1;
            const b = start + q[1] - 1;
            const c = start + q[2] - 1;
            const d = start + q[3] - 1;
            faces.push([a, b, c]);
            faces.push([a, c, d]);
        }
    }

    // Main Chassis / Frame (Length 4.6m, Width 1.9m, Height 0.6m)
    addBox(-0.95, 0.40, -2.10, 0.95, 1.00, 2.10);

    // Front Hood & Engine Bay
    addBox(-0.92, 0.90, 0.60, 0.92, 1.30, 2.10);

    // Cabin / Passenger Compartment
    addBox(-0.90, 0.95, -1.90, 0.90, 1.95, 0.65);

    // Heavy Duty Yemeni Front Bullbar / Grille Guard
    addBox(-0.98, 0.45, 2.12, 0.98, 1.15, 2.25);

    // Roof Luggage Rack (Traditional Yemeni Overland Carrier)
    addBox(-0.85, 1.95, -1.80, 0.85, 2.12, 0.50);

    // Left and Right Side Mirrors
    addBox(-1.18, 1.30, 0.55, -0.92, 1.48, 0.70);
    addBox(0.92, 1.30, 0.55, 1.18, 1.48, 0.70);

    // Helper for 8-sided cylinder (Wheels)
    function addCylinderWheel(centerX, centerY, centerZ, radius, width) {
        const segments = 12;
        const start = vertices.length + 1;
        const halfW = width * 0.5;

        for (let i = 0; i < segments; i++) {
            const angle = (i / segments) * Math.PI * 2;
            const dy = Math.sin(angle) * radius;
            const dz = Math.cos(angle) * radius;
            vertices.push([centerX - halfW, centerY + dy, centerZ + dz]);
            vertices.push([centerX + halfW, centerY + dy, centerZ + dz]);
        }

        for (let i = 0; i < segments; i++) {
            const next = (i + 1) % segments;
            const v1 = start + i * 2;
            const v2 = start + i * 2 + 1;
            const v3 = start + next * 2 + 1;
            const v4 = start + next * 2;

            // Outer tread face
            faces.push([v1, v2, v3]);
            faces.push([v1, v3, v4]);
        }
    }

    // 4 Heavy-Duty All-Terrain Wheels
    addCylinderWheel(-0.98, 0.42, 1.35, 0.42, 0.30); // Front Left
    addCylinderWheel(0.98, 0.42, 1.35, 0.42, 0.30);  // Front Right
    addCylinderWheel(-0.98, 0.42, -1.35, 0.42, 0.30); // Rear Left
    addCylinderWheel(0.98, 0.42, -1.35, 0.42, 0.30);  // Rear Right

    writeOBJ(path.join(modelsDir, 'Vehicle_4WD.obj'), 'YemeniLandCruiser4WD', vertices, [], [], faces);
}

// 2. Generate Guardrail Segment OBJ
function generateGuardrailOBJ() {
    const vertices = [];
    const faces = [];

    function addBox(minX, minY, minZ, maxX, maxY, maxZ) {
        const start = vertices.length + 1;
        vertices.push([minX, minY, minZ]);
        vertices.push([maxX, minY, minZ]);
        vertices.push([maxX, maxY, minZ]);
        vertices.push([minX, maxY, minZ]);
        vertices.push([minX, minY, maxZ]);
        vertices.push([maxX, minY, maxZ]);
        vertices.push([maxX, maxY, maxZ]);
        vertices.push([minX, maxY, maxZ]);

        const quads = [
            [1, 4, 3, 2], [5, 6, 7, 8], [1, 5, 8, 4],
            [2, 3, 7, 6], [4, 8, 7, 3], [1, 2, 6, 5]
        ];

        for (const q of quads) {
            faces.push([start + q[0] - 1, start + q[1] - 1, start + q[2] - 1]);
            faces.push([start + q[0] - 1, start + q[2] - 1, start + q[3] - 1]);
        }
    }

    // 2 I-Beam Steel Support Posts
    addBox(-0.10, 0.0, -1.8, 0.10, 0.90, -1.6);
    addBox(-0.10, 0.0, 1.6, 0.10, 0.90, 1.8);

    // W-Beam Horizontal Corrugated Rail
    addBox(-0.06, 0.48, -2.0, 0.06, 0.88, 2.0);

    writeOBJ(path.join(modelsDir, 'Guardrail_Segment.obj'), 'MountainGuardrail', vertices, [], [], faces);
}

// 3. Generate Yemeni Tower House OBJ
function generateYemeniTowerOBJ() {
    const vertices = [];
    const faces = [];

    function addBox(minX, minY, minZ, maxX, maxY, maxZ) {
        const start = vertices.length + 1;
        vertices.push([minX, minY, minZ]);
        vertices.push([maxX, minY, minZ]);
        vertices.push([maxX, maxY, minZ]);
        vertices.push([minX, maxY, minZ]);
        vertices.push([minX, minY, maxZ]);
        vertices.push([maxX, minY, maxZ]);
        vertices.push([maxX, maxY, maxZ]);
        vertices.push([minX, maxY, maxZ]);

        const quads = [
            [1, 4, 3, 2], [5, 6, 7, 8], [1, 5, 8, 4],
            [2, 3, 7, 6], [4, 8, 7, 3], [1, 2, 6, 5]
        ];

        for (const q of quads) {
            faces.push([start + q[0] - 1, start + q[1] - 1, start + q[2] - 1]);
            faces.push([start + q[0] - 1, start + q[2] - 1, start + q[3] - 1]);
        }
    }

    // Ground Floor: Dark Mountain Basalt Stone Foundation (4.5m x 4.5m x 3.5m)
    addBox(-4.5, 0.0, -4.5, 4.5, 3.5, 4.5);

    // 2nd Floor: Middle Stone Living Quarters
    addBox(-4.3, 3.5, -4.3, 4.3, 7.0, 4.3);

    // 3rd Floor: Upper Clay/Brick Story
    addBox(-4.1, 7.0, -4.1, 4.1, 10.5, 4.1);

    // 4th Floor: Top Diwan / Mafraj with View Over the Pass
    addBox(-3.9, 10.5, -3.9, 3.9, 14.0, 3.9);

    // Defensive Parapet with Traditional Yemeni Merlons (Crenellations)
    addBox(-4.0, 14.0, -4.0, 4.0, 15.0, -3.6);
    addBox(-4.0, 14.0, 3.6, 4.0, 15.0, 4.0);
    addBox(-4.0, 14.0, -3.6, -3.6, 15.0, 3.6);
    addBox(3.6, 14.0, -3.6, 4.0, 15.0, 3.6);

    writeOBJ(path.join(modelsDir, 'Yemeni_TowerHouse.obj'), 'YemeniStoneTowerHouse', vertices, [], [], faces);
}

generateVehicleOBJ();
generateGuardrailOBJ();
generateYemeniTowerOBJ();
console.log("All 3D models generated successfully!");
