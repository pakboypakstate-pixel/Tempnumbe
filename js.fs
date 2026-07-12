// server.js
// Express server that:
// 1. Exposes a webhook endpoint (/webhook/sms) that Twilio calls whenever
//    an SMS is received on your Twilio number.
// 2. Stores incoming messages in memory.
// 3. Exposes a GET /api/messages endpoint so the front-end (index.html)
//    can poll and display them.

require('dotenv').config();
const express = require('express');
const twilio = require('twilio');

const app = express();
const PORT = process.env.PORT || 3000;

// Twilio sends webhook data as application/x-www-form-urlencoded
app.use(express.urlencoded({ extended: false }));
app.use(express.json());
app.use(express.static('public')); // serves index.html

// In-memory store for received messages (swap for a real DB in production)
let messages = [];

// --- Optional but recommended: validate that the request really came from Twilio ---
function validateTwilioRequest(req, res, next) {
  const authToken = process.env.TWILIO_AUTH_TOKEN;

  // Skip validation if no auth token is configured (e.g. local testing without ngrok+Twilio)
  if (!authToken) return next();

  const twilioSignature = req.headers['x-twilio-signature'];
  const url = `${process.env.PUBLIC_URL}/webhook/sms`; // must match the exact URL Twilio calls

  const isValid = twilio.validateRequest(
    authToken,
    twilioSignature,
    url,
    req.body
  );

  if (!isValid) {
    console.warn('Rejected request with invalid Twilio signature');
    return res.status(403).send('Invalid signature');
  }

  next();
}

// --- Webhook: Twilio POSTs here on every inbound SMS ---
app.post('/webhook/sms', validateTwilioRequest, (req, res) => {
  const { From, To, Body, MessageSid } = req.body;

  const incoming = {
    sid: MessageSid,
    from: From,
    to: To,
    body: Body,
    receivedAt: new Date().toISOString(),
  };

  messages.unshift(incoming); // newest first
  console.log('Received SMS:', incoming);

  // Respond to Twilio with empty TwiML so it knows we handled it
  // (no auto-reply sent back to the sender)
  const twiml = new twilio.twiml.MessagingResponse();
  res.type('text/xml').send(twiml.toString());
});

// --- API for the front-end to poll ---
app.get('/api/messages', (req, res) => {
  res.json(messages);
});

app.listen(PORT, () => {
  console.log(`Server running on http://localhost:${PORT}`);
  console.log(`Webhook URL to give Twilio: <your-public-url>/webhook/sms`);
});
