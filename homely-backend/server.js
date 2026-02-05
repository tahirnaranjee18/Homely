// 1. Import Packages
const express = require('express');
const bodyParser = require('body-parser');
const cors = require('cors');
const mongoose = require('mongoose');

// 2. Setup Express App
const app = express();
const port = 8080;
app.use(cors());
app.use(bodyParser.json());

// 3. Connect to MongoDB Atlas
// Replace <username> with your database username.
// Replace <password> with the password you copied.
const connectionString = "mongodb+srv://homelyAppUser:Homely123@cluster0.pbyrcie.mongodb.net/?retryWrites=true&w=majority&appName=Cluster0";

mongoose.connect(connectionString)
    .then(() => console.log("✅ MongoDB connected successfully"))
    .catch(err => console.error("❌ MongoDB connection error:", err));

// 4. Define the Data Structure (Schema & Model)
const propertySchema = new mongoose.Schema({
    ownerId: String,
    title: String,
    location: String,
    price: String,
    // Add any other fields from your Property.kt model here
});
const Property = mongoose.model('Property', propertySchema);

// 5. Update API Endpoints to use MongoDB
// --- GET /properties ---
app.get('/properties', async (req, res) => {
    try {
        const properties = await Property.find(); // Fetches all properties from the database
        res.json(properties);
    } catch (err) {
        res.status(500).json({ message: err.message });
    }
});

// --- POST /properties ---
app.post('/properties', async (req, res) => {
    const property = new Property({
        ownerId: req.body.ownerId,
        title: req.body.title,
        location: req.body.location,
        price: req.body.price,
    });

    try {
        const newProperty = await property.save(); // Saves the new property to the database
        res.status(201).json(newProperty);
    } catch (err) {
        res.status(400).json({ message: err.message });
    }
});

// --- POST /maintenance (Example for another collection) ---
// You would create a new schema and model for maintenance requests as well.
app.post('/maintenance', (req, res) => {
    console.log("Received maintenance request:", req.body);
    // TODO: Create a Maintenance schema/model and save the request
    res.status(201).send();
});

// 6. Start the Server
app.listen(port, () => {
    console.log(`✅ Server is running on http://localhost:${port}`);
});