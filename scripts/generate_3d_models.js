/**
 * 3D Model Generator for Nageel Yemeni Mountain Driving - Phase 2
 * Generates detailed Wavefront OBJ models:
 * 1. Yemeni 4WD Mountain SUV (Chassis, Cabin, Hood, Bullbar, Roof Rack, 4WD Wheels)
 * 2. Yemeni Mountain Touring Sedan (Aerodynamic Sedan Chassis, Cabin, Trunk, Low-profile Wheels)
 * 3. Yemeni Pickup Truck / Shas (Reinforced Cab, Heavy-Duty Cargo Bed, Roll Bar, High Clearance Wheels)
 * 4. Mountain AI Traffic Car (Compact Yemeni Taxi / Commuter)
 * 5. Yemeni Green Highland Agricultural Terrace (Stepped Stone Retaining Walls)
 * 6. Yemeni Wadi Canyon Boulder & Rock Outcrop (Natural Valley Obstacles)
 * 7. Mountain Highway Guardrail Segment (Steel I-Posts, W-Beam rail)
 * 8. Traditional Yemeni Stone Tower House (Multi-tier stone masonry with parapets)
 */

const fs = require('fs');
const path = require('path');

const modelsDir = path.join(__dirname, '..', 'Assets', 'Models');
if (!fs.existsSync(modelsDir)) {
    fs.mkdirSync(modelsDir, { recursive: true });
}

function writeOBJ(filePath, name, vertices, normals, uvs, faces) {
    let out = `# Wavefront OBJ: ${name}\n# Generated for Nageel - Yemeni Mountain Driving Phase 2\no ${name}\n\n`;

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

function createBoxHelper(vertices, faces) {
    return function addBox(minX, minY, minZ, maxX, maxY, maxZ) {
        const start = vertices.length + 1;
        vertices.push([minX, minY, minZ]); // 1
        vertices.push([maxX, minY, minZ]); // 2
        vertices.push([maxX, maxY, minZ]); // 3
        vertices.push([minX, maxY, minZ]); // 4
        vertices.push([minX, minY, maxZ]); // 5
        vertices.push([maxX, minY, maxZ]); // 6
        vertices.push([maxX, maxY, maxZ]); // 7
        vertices.push([minX, maxY, maxZ]); // 8

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
    };
}

function createCylinderWheelHelper(vertices, faces) {
    return function addCylinderWheel(centerX, centerY, centerZ, radius, width, segments = 12) {
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
    };
}

// 1. Generate 4WD SUV Model (Land Cruiser Mountain Edition)
function generateVehicle4WDOBJ() {
    const vertices = [];
    const faces = [];
    const addBox = createBoxHelper(vertices, faces);
    const addWheel = createCylinderWheelHelper(vertices, faces);

    // Main Chassis
    addBox(-0.95, 0.40, -2.10, 0.95, 1.00, 2.10);
    // Front Hood & Engine Bay
    addBox(-0.92, 0.90, 0.60, 0.92, 1.30, 2.10);
    // Cabin / Passenger Compartment
    addBox(-0.90, 0.95, -1.90, 0.90, 1.95, 0.65);
    // Cabin Glass / Windows
    addBox(-0.86, 1.35, -1.82, 0.86, 1.88, 0.55);
    // Yemeni Front Bullbar / Grille Guard
    addBox(-0.98, 0.45, 2.12, 0.98, 1.15, 2.25);
    // Roof Luggage Rack
    addBox(-0.85, 1.95, -1.80, 0.85, 2.12, 0.50);
    // Side Mirrors
    addBox(-1.18, 1.30, 0.55, -0.92, 1.48, 0.70);
    addBox(0.92, 1.30, 0.55, 1.18, 1.48, 0.70);
    // Rear Mounted Spare Tire
    addBox(-0.35, 0.70, -2.35, 0.35, 1.40, -2.12);

    // 4 Wheels
    addWheel(-0.98, 0.42, 1.35, 0.42, 0.30);
    addWheel(0.98, 0.42, 1.35, 0.42, 0.30);
    addWheel(-0.98, 0.42, -1.35, 0.42, 0.30);
    addWheel(0.98, 0.42, -1.35, 0.42, 0.30);

    writeOBJ(path.join(modelsDir, 'Vehicle_4WD.obj'), 'YemeniLandCruiser4WD', vertices, [], [], faces);
}

// 2. Generate Sedan Model (Mountain Touring Sedan)
function generateVehicleSedanOBJ() {
    const vertices = [];
    const faces = [];
    const addBox = createBoxHelper(vertices, faces);
    const addWheel = createCylinderWheelHelper(vertices, faces);

    // Lower Sleek Chassis (Length 4.7m, Width 1.82m, Ground clearance 0.22m)
    addBox(-0.91, 0.22, -2.25, 0.91, 0.75, 2.25);
    // Front Low Sloping Hood
    addBox(-0.88, 0.70, 0.70, 0.88, 0.98, 2.20);
    // Front Aerodynamic Bumper & Foglights
    addBox(-0.92, 0.20, 2.20, 0.92, 0.65, 2.38);
    // Aerodynamic Greenhouse / Cabin
    addBox(-0.82, 0.75, -1.25, 0.82, 1.48, 0.75);
    // Sloped Rear Trunk
    addBox(-0.86, 0.70, -2.25, 0.86, 0.96, -1.25);
    // Subtle Rear Lip Spoiler
    addBox(-0.84, 0.96, -2.28, 0.84, 1.04, -2.12);
    // Sleek Side Mirrors
    addBox(-1.12, 0.95, 0.65, -0.84, 1.10, 0.80);
    addBox(0.84, 0.95, 0.65, 1.12, 1.10, 0.80);

    // 4 Low-Profile Alloy Road Wheels (Radius 0.33m)
    addWheel(-0.93, 0.33, 1.40, 0.33, 0.26);
    addWheel(0.93, 0.33, 1.40, 0.33, 0.26);
    addWheel(-0.93, 0.33, -1.40, 0.33, 0.26);
    addWheel(0.93, 0.33, -1.40, 0.33, 0.26);

    writeOBJ(path.join(modelsDir, 'Vehicle_Sedan.obj'), 'MountainSedan', vertices, [], [], faces);
}

// 3. Generate Pickup Truck Model (Yemeni Shas / Hilux 4x4)
function generateVehiclePickupOBJ() {
    const vertices = [];
    const faces = [];
    const addBox = createBoxHelper(vertices, faces);
    const addWheel = createCylinderWheelHelper(vertices, faces);

    // Heavy-duty Truck Chassis
    addBox(-0.94, 0.42, -2.40, 0.94, 0.95, 2.35);
    // Front Hood & Grille
    addBox(-0.90, 0.90, 0.65, 0.90, 1.34, 2.30);
    // Front Heavy-duty Steel Bullbar & Tow Hook
    addBox(-0.96, 0.40, 2.32, 0.96, 1.20, 2.50);
    // Single / Extended Cab (Driver compartment)
    addBox(-0.88, 0.95, -0.45, 0.88, 1.88, 0.70);
    // Rear Bed Floor & Sidewalls (Traditional Yemeni Shas Bed)
    addBox(-0.92, 0.85, -2.45, 0.92, 1.25, -0.50); // Bed base & walls
    // Heavy Tubular Roll Bar Behind Cabin
    addBox(-0.84, 1.25, -0.58, 0.84, 1.85, -0.48);
    // Tailgate
    addBox(-0.92, 0.85, -2.52, 0.92, 1.25, -2.44);
    // Off-road Snorkel Intake (A-pillar)
    addBox(0.89, 0.90, 0.60, 0.98, 1.95, 0.72);
    // Mirrors
    addBox(-1.18, 1.25, 0.58, -0.90, 1.42, 0.72);
    addBox(0.90, 1.25, 0.58, 1.18, 1.42, 0.72);

    // 4 High-Clearance Mud-Terrain Wheels
    addWheel(-1.00, 0.44, 1.45, 0.44, 0.32);
    addWheel(1.00, 0.44, 1.45, 0.44, 0.32);
    addWheel(-1.00, 0.44, -1.45, 0.44, 0.32);
    addWheel(1.00, 0.44, -1.45, 0.44, 0.32);

    writeOBJ(path.join(modelsDir, 'Vehicle_Pickup.obj'), 'YemeniPickupShas', vertices, [], [], faces);
}

// 4. Generate AI Traffic Car (Compact Mountain Taxi)
function generateTrafficCarOBJ() {
    const vertices = [];
    const faces = [];
    const addBox = createBoxHelper(vertices, faces);
    const addWheel = createCylinderWheelHelper(vertices, faces);

    // Compact Body
    addBox(-0.85, 0.30, -1.85, 0.85, 0.85, 1.85);
    // Hood
    addBox(-0.82, 0.80, 0.45, 0.82, 1.10, 1.80);
    // Cabin
    addBox(-0.78, 0.82, -1.25, 0.78, 1.55, 0.50);
    // Taxi Roof Sign
    addBox(-0.35, 1.55, -0.20, 0.35, 1.72, 0.15);

    // 4 Standard Wheels
    addWheel(-0.88, 0.35, 1.15, 0.35, 0.24);
    addWheel(0.88, 0.35, 1.15, 0.35, 0.24);
    addWheel(-0.88, 0.35, -1.15, 0.35, 0.24);
    addWheel(0.88, 0.35, -1.15, 0.35, 0.24);

    writeOBJ(path.join(modelsDir, 'Traffic_Car.obj'), 'AITrafficCar', vertices, [], [], faces);
}

// 5. Generate Yemeni Green Highland Agricultural Terrace (Tiered stone farming levels)
function generateYemeniTerraceOBJ() {
    const vertices = [];
    const faces = [];
    const addBox = createBoxHelper(vertices, faces);

    // 3 Curved / Stepped Retaining Walls and Green Plant Beds
    // Tier 1 (Lowest)
    addBox(-18.0, 0.0, -10.0, 18.0, 2.5, -4.0); // Stone Wall
    addBox(-17.5, 2.2, -4.0, 17.5, 2.5, 4.0);   // Green Soil Terrace

    // Tier 2 (Middle)
    addBox(-16.0, 2.5, 4.0, 16.0, 5.5, 9.0);   // Stone Wall
    addBox(-15.5, 5.2, 9.0, 15.5, 5.5, 17.0);  // Green Soil Terrace

    // Tier 3 (Upper)
    addBox(-14.0, 5.5, 17.0, 14.0, 8.5, 22.0); // Stone Wall
    addBox(-13.5, 8.2, 22.0, 13.5, 8.5, 30.0); // Green Soil Terrace

    writeOBJ(path.join(modelsDir, 'Yemeni_Terrace.obj'), 'YemeniAgriculturalTerrace', vertices, [], [], faces);
}

// 6. Generate Wadi Canyon Boulder & Rock Outcrop (Natural obstacles in Rocky Valley)
function generateWadiRockOBJ() {
    const vertices = [];
    const faces = [];
    const addBox = createBoxHelper(vertices, faces);

    // Main jagged boulder core
    addBox(-2.5, 0.0, -2.8, 2.5, 2.8, 2.8);
    // Asymmetric rock slabs simulating weathered wadi cliff fallen stones
    addBox(-3.2, 0.2, -1.8, -2.0, 1.9, 2.2);
    addBox(1.8, 0.1, -2.2, 3.4, 2.1, 1.4);
    addBox(-1.6, 2.5, -1.9, 1.8, 3.8, 1.7);
    addBox(-2.2, 0.0, 2.4, 2.0, 1.5, 3.8);

    writeOBJ(path.join(modelsDir, 'Yemeni_Wadi_Rock.obj'), 'YemeniWadiBoulder', vertices, [], [], faces);
}

// 7. Generate Guardrail Segment OBJ
function generateGuardrailOBJ() {
    const vertices = [];
    const faces = [];
    const addBox = createBoxHelper(vertices, faces);

    // 2 I-Beam Steel Support Posts
    addBox(-0.10, 0.0, -1.8, 0.10, 0.90, -1.6);
    addBox(-0.10, 0.0, 1.6, 0.10, 0.90, 1.8);

    // W-Beam Horizontal Corrugated Rail
    addBox(-0.06, 0.48, -2.0, 0.06, 0.88, 2.0);

    writeOBJ(path.join(modelsDir, 'Guardrail_Segment.obj'), 'MountainGuardrail', vertices, [], [], faces);
}

// 8. Generate Yemeni Tower House OBJ
function generateYemeniTowerOBJ() {
    const vertices = [];
    const faces = [];
    const addBox = createBoxHelper(vertices, faces);

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

// Run generators
generateVehicle4WDOBJ();
generateVehicleSedanOBJ();
generateVehiclePickupOBJ();
generateTrafficCarOBJ();
generateYemeniTerraceOBJ();
generateWadiRockOBJ();
generateGuardrailOBJ();
generateYemeniTowerOBJ();

console.log("All Phase 2 3D models generated successfully!");
