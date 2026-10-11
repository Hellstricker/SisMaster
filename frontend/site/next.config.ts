import type { NextConfig } from 'next';

const config: NextConfig = {
  // Servidor Node enxuto para o Docker (copia só o que precisa).
  output: 'standalone',
  poweredByHeader: false,
};

export default config;
