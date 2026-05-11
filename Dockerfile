FROM node:20-alpine
WORKDIR /app
COPY package.json server.js ./
COPY public ./public
EXPOSE 3000
CMD ["node", "server.js"]
