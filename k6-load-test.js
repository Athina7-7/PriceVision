import http from 'k6/http';
import { check, sleep } from 'k6';

// Configuración de la prueba de carga
export const options = {
  stages: [
    { duration: '10s', target: 10 }, // Rampa de subida a 10 usuarios concurrentes
    { duration: '30s', target: 10 }, // Mantiene 10 usuarios por 30s
    { duration: '10s', target: 0 },  // Rampa de bajada final
  ],
  thresholds: {
    // El 95% de las peticiones deben completarse en menos de 5 segundos
    http_req_duration: ['p(95)<5000'],
  },
};

const BASE_URL = 'http://localhost:5054/api';

export function setup() {
  const res = http.post(`${BASE_URL}/auth/login`, JSON.stringify({ username: 'admin', password: 'Admin123!' }), {
    headers: { 'Content-Type': 'application/json' }
  });
  return { token: res.json('token') };
}

export default function (data) {
  const params = { headers: { 'Authorization': `Bearer ${data.token}` } };

  // 1. Simulamos la carga inicial del dashboard que realiza el frontend en paralelo
  const responses = http.batch([
    ['GET', `${BASE_URL}/projects?take=30`, null, params],
    ['GET', `${BASE_URL}/predictions?take=8`, null, params],
    ['GET', `${BASE_URL}/evm/recent?take=8`, null, params],
    ['GET', `${BASE_URL}/financial-predictions?take=8`, null, params],
    ['GET', `${BASE_URL}/financial-predictions/history`, null, params]
  ]);

  check(responses[0], { 'status is 200 (projects)': (r) => r.status === 200 });
  check(responses[1], { 'status is 200 (predictions)': (r) => r.status === 200 });
  check(responses[2], { 'status is 200 (evm)': (r) => r.status === 200 });
  check(responses[3], { 'status is 200 (financial)': (r) => r.status === 200 });
  check(responses[4], { 'status is 200 (history)': (r) => r.status === 200 });

  // 2. Simulamos la visualización del historial de un proyecto
  let projects = [];
  try { projects = JSON.parse(responses[0].body); } catch (e) {}

  if (projects && projects.length > 0) {
    const projectId = projects[0].projectId;
    const resHistory = http.get(`${BASE_URL}/projects/${projectId}/history`, params);
    check(resHistory, { 'status is 200 (history)': (r) => r.status === 200 });
  }

  // Simular el tiempo natural de espera entre interacciones
  sleep(1);
}